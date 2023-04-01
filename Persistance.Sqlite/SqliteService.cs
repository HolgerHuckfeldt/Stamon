using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Persistance.Sqlite.Model;
using Stamon.Types.Interfaces;
using Stamon.Types.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.Resources;
using System.Text;
using System.Threading.Tasks;
using DbConnection = Microsoft.Data.Sqlite.SqliteConnection;
using DbCommand = Microsoft.Data.Sqlite.SqliteCommand;
using System.Threading;
using Persistance.Sqlite.Services;
using Monitor = Stamon.Types.Model.Monitor;
using System.Linq;

namespace Persistance.Sqlite
{
    internal class SqliteService : IStamonDatabasePersistance
    {
        public const int SchemaVersion = 1;

        private readonly SqliteCacheOptions _config;
        private readonly ILogger _logger;
        private readonly DbConnection _db;
        private readonly ItemCacheService _itemService;

        private static ConcurrentDictionary<string, SlotCache> _slotCache = new ConcurrentDictionary<string, SlotCache>();


        private DbCommandPool Commands { get; }

        static SqliteService()
        {
            SQLitePCL.Batteries.Init();
        }

        public SqliteService(
            IOptions<SqliteCacheOptions> options,
            ItemCacheService itemService,
            ILogger<SqliteService>? logger = null)
            : this(options.Value, itemService, logger)
        {
        }

        public SqliteService(
            SqliteCacheOptions options,
            ItemCacheService itemService,
            ILogger<SqliteService>? logger = null)
        {
            _config = options;
            _logger = logger ?? new NullLogger<SqliteService>();
            _itemService = itemService;

            _db = Connect(_config, _logger);
            Commands = new DbCommandPool(_db, _logger);

        }

        public void Dispose()
        {
            _logger.LogTrace("Disposing SQLite cache database at {SqliteCacheDbPath}", _config.CachePath);
            //_cleanupTimer?.Dispose();
            Commands.Dispose();

            _logger.LogTrace("Closing connection to SQLite database at {SqliteCacheDbPath}", _config.CachePath);
            _db.Close();
            _db.Dispose();
        }

        #region Database Connection Initialization
        private static bool CheckExistingDb(DbConnection db, ILogger logger)
        {
            try
            {
                // Check for correct structure
                using (var cmd = new DbCommand(@"SELECT COUNT(*) from sqlite_master", db))
                {
                    var result = (long)cmd.ExecuteScalar()!;
                    // We are expecting two tables and one additional index
                    if (result < 8)
                    {
                        logger.LogWarning("Incorrect/incompatible existing cache db structure found!");
                        return false;
                    }
                }

                // Check for correct version
                using (var cmd = new DbCommand(@"SELECT value FROM meta WHERE key = ""version""", db))
                {
                    var result = (long)cmd.ExecuteScalar()!;
                    if (result != SchemaVersion)
                    {
                        logger.LogWarning("Existing cache db has unsupported schema version {SchemaVersion}",
                            result);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while checking compatibility of existing cache db!");
                return false;
            }

            return true;
        }

        private static DbConnection Connect(SqliteCacheOptions config, ILogger logger)
        {
            var connectionString = config.ConnectionString;
            logger.LogTrace("Opening connection to SQLite database: " +
                "{ConnectionString}", connectionString);

            DbConnection? db = null;

            // First try to open an existing database
            if (!config.MemoryOnly && System.IO.File.Exists(config.CachePath))
            {
                logger.LogTrace("Found existing database at {CachePath}", config.CachePath);

                db = new DbConnection(config.ConnectionString);
                db.Open();

                if (!CheckExistingDb(db, logger))
                {
                    logger.LogTrace("Closing connection to SQLite database at {SqliteCacheDbPath}", config.CachePath);
                    db.Close();
                    db.Dispose();
                    db = null;

                    logger.LogInformation("Deleting existing incompatible cache db file {CachePath}", config.CachePath);
                    System.IO.File.Delete(config.CachePath);
                }
            }

            if (db is null)
            {
                db = new DbConnection(config.ConnectionString);
                db.Open();
                Initialize(config, db, logger);
            }

            // Explicitly set default journal mode and fsync behavior
            using (var cmd = new DbCommand("PRAGMA journal_mode = WAL;", db))
            {
                cmd.ExecuteNonQuery();
            }
            using (var cmd = new DbCommand("PRAGMA synchronous = NORMAL;", db))
            {
                cmd.ExecuteNonQuery();
            }

            return db;
        }

        private static void Initialize(SqliteCacheOptions config, DbConnection db, ILogger logger)
        {
            logger.LogInformation("Initializing db cache: {ConnectionString}",
                config.ConnectionString);

            using var transaction = db.BeginTransaction();
            using (var cmd = new DbCommand(DbCommands.Commands[(int)Operation.CreateDb], db))
            {
                cmd.Transaction = transaction;
                cmd.ExecuteNonQuery();
            }
            using (var cmd = new DbCommand(
                       $"INSERT INTO Meta (Key, Value) " +
                       $"VALUES " +
                       $@"(""version"", {SchemaVersion}), " +
                       $@"(""created"", {DateTimeOffset.UtcNow.Ticks})", db))
            {
                cmd.Transaction = transaction;
                cmd.ExecuteNonQuery();
            }
            transaction.Commit();
        }

        #endregion

        public async Task AddMonitor(Stamon.Types.Model.Monitor monitor)
        {
            try
            {
                long? slotid;

                if (!_slotCache.TryGetValue(monitor.Name + monitor.Scale, out var slot))
                {
                    // Get Slot from Database
                    slotid = await GetSlotId(monitor);

                    // Cache it for next time.
                    _slotCache.TryAdd(monitor.Name + monitor.Scale,
                        new SlotCache() { Scale = monitor.Scale, Name = monitor.Name, SlotId = slotid });
                }
                else
                {
                    slotid = slot.SlotId;
                }

                if (slotid != null)
                {
                    var scaledTime = GetScaledTime(monitor.Scale, DateTime.Now);

                    // Get Item from Cache.
                    var cacheItem = _itemService.GetLastItemForScale(slotid.Value);
                    if (cacheItem == null)
                    {
                        // Get Item from Database.
                        cacheItem = await GetMaxItemFromDb(slotid.Value);
                    }

                    if (cacheItem!=null)
                    {
                        if (cacheItem.LastInsert == scaledTime)
                        {
                            cacheItem.Count++;

                            //"UPDATE Items SET Count = @count WHERE Id = @id";
                            Commands.Use(Operation.UpdateItemCount, cmd =>
                            {
                                cmd.Parameters.AddWithValue("@count", cacheItem.Count);
                                cmd.Parameters.AddWithValue("@id", cacheItem.ItemId);

                                cmd.ExecuteNonQuery();
                            });

                        }
                        else
                        {
                            var newScaledTime = GetScaledTime(monitor.Scale, DateTime.Now);

                            //"INSERT INTO Items (Count, Time, SlotId) VALUES ( @count, @time, @slotid)";
                            Commands.Use(Operation.InsertItem, cmd =>
                            {
                                cmd.Parameters.AddWithValue("@count", 1);
                                cmd.Parameters.AddWithValue("@time", newScaledTime);
                                cmd.Parameters.AddWithValue("@slotid", slotid);

                                cmd.ExecuteNonQuery();
                            });

                            var lastItem = await GetMaxItemFromDb(slotid.Value);

                            var newItem = new ItemCache()
                            {
                                Count = lastItem.Count,
                                SlotId = slotid.Value,
                                LastInsert = newScaledTime,
                                ItemId = lastItem.ItemId
                            };

                            _itemService.AddScaleItem(newItem);
                        }
                    }
                }
                else
                {
                    try
                    {
                        //"INSERT OR REPLACE INTO Slot (Name, Duration, Created, Scale) " +
                        //"VALUES (@name, @duration, @created, @scale)";
                        Commands.Use(Operation.InsertSlot, cmd =>
                        {
                            cmd.Parameters.AddWithValue("@name", monitor.Name);
                            cmd.Parameters.AddWithValue("@duration", monitor.Duration);
                            cmd.Parameters.AddWithValue("@created", DateTime.Now);
                            cmd.Parameters.AddWithValue("@scale", monitor.Scale);
                            cmd.ExecuteNonQuery();
                        });

                        var newSlotid = (long?)Commands.Use(Operation.GetSlot, cmd =>
                        {
                            cmd.Parameters.AddWithValue("@name", monitor.Name);
                            cmd.Parameters.AddWithValue("@scale", monitor.Scale);
                            return cmd.ExecuteScalar();
                        })!;

                        // Cache it for next time.
                        _slotCache.TryAdd(monitor.Name + monitor.Scale,
                            new SlotCache() { Scale = monitor.Scale, Name = monitor.Name, SlotId = newSlotid });

                        //"INSERT INTO Items ([Count], [Time], [SlotId]) VALUES ( @count, @time, @slotid)";
                        Commands.Use(Operation.InsertItem, cmd =>
                        {
                            cmd.Parameters.AddWithValue("@count", 1);
                            cmd.Parameters.AddWithValue("@time", GetScaledTime(monitor.Scale, DateTime.Now));
                            cmd.Parameters.AddWithValue("@slotid", newSlotid);

                            cmd.ExecuteNonQuery();
                        });

                        var lastItem = await GetMaxItemFromDb(slotid.Value);

                        var newItem = new ItemCache()
                        {
                            Count = lastItem.Count,
                            SlotId = slotid.Value,
                            LastInsert = lastItem.LastInsert,
                            ItemId = lastItem.ItemId
                        };

                        _itemService.AddScaleItem(newItem);

                    }
                    catch (Exception ex)
                    {

                    }
                }
            }
            catch (Exception ex)
            {

            }
        }

        public async Task<ICollection<ActionPerTime>> GetMonitor(Stamon.Types.Model.Monitor monitor)
        {
            return await GetMonitor(
                monitor,
                GetScaledTime(monitor.Scale, DateTime.Now, monitor.Duration),
                GetScaledTime(monitor.Scale, DateTime.Now, 0));
        }

        public async Task<ICollection<ActionPerTime>> GetMonitor(Stamon.Types.Model.Monitor monitor, DateTime from, DateTime to)
        {
            var slotid = (long?)Commands.Use(Operation.GetSlot, cmd =>
            {
                cmd.Parameters.AddWithValue("@name", monitor.Name);
                cmd.Parameters.AddWithValue("@scale", monitor.Scale);
                return cmd.ExecuteScalar();
            })!;

            if (slotid != null)
            {
                var itemdata = await(Task<LinkedList<ActionPerTime>>)Commands.Use(Operation.GetItemWithDate, async cmd =>
                {
                    cmd.Parameters.AddWithValue("@slot", slotid.Value);
                    cmd.Parameters.AddWithValue("@from", from);
                    cmd.Parameters.AddWithValue("@to", to);

                    await using var reader = await cmd.ExecuteReaderAsync();

                    LinkedList<ActionPerTime> result = new LinkedList<ActionPerTime>();

                    ActionPerTime lastItem = null;

                    while (reader.Read())
                    {
                        long count = (long)reader[0];
                        var dt = reader.GetDateTime(1);

                        var newItem = new ActionPerTime() { Count = count, Time = dt };

                        if (lastItem == null)
                        {
                            lastItem = newItem;
                        }
                        else
                        {
                            if (newItem.Time > lastItem.Time)
                            {
                                result.AddLast(lastItem);
                                lastItem = newItem;
                            }
                            else
                            {
                                lastItem.Count += newItem.Count;
                            }
                        }
                        
                    }
                    if(lastItem != null)
                        result.AddLast(lastItem);

                    reader.Close();

                    return result;
                });

                return NormalizeList(monitor.Scale, monitor.Duration, itemdata);
            }

            return new List<ActionPerTime>();
        }

        public Task<ICollection<string>> GetMonitorNames()
        {
            var names = (Task<ICollection<string>>)Commands.Use(Operation.GetMonitorNames, async cmd =>
            {
                var reader = await cmd.ExecuteReaderAsync();
                var result = new List<string>();
                while (reader.Read())
                {
                    result.Add(result[0]);
                }

                return result as ICollection<string>;
            })!;

            return names;
        }


        private async Task<long?> GetSlotId(Monitor monitor)
        {
            var slotid = (long?)Commands.Use(Operation.GetSlot, cmd =>
            {
                cmd.Parameters.AddWithValue("@name", monitor.Name);
                cmd.Parameters.AddWithValue("@scale", monitor.Scale);
                return cmd.ExecuteScalar();
            })!;

            return slotid;
        }

        private async Task<ItemCache> GetMaxItemFromDb(long slotId)
        {
            // Get Item from Database.
            var itemdata = await(Task<ItemCache?>)Commands.Use(Operation.GetItem, async cmd =>
            {
                try
                {
                    cmd.Parameters.AddWithValue("@slot", slotId);

                    await using var reader = await cmd.ExecuteReaderAsync();
                    while (reader.Read())
                    {
                        DateTime? dt = null;
                        if (!reader.IsDBNull(0))
                            dt = reader.GetDateTime(0);
                        long? count = (long)reader[1];
                        long? id = (long)reader[2];
                        reader.Close();
                        if (dt != null)
                            return new ItemCache() { Count = count.Value, LastInsert = dt.Value, ItemId = id.Value };
                        return null;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, ex.Message);
                }

                return null;
            })!;

            return itemdata;
        }

        private DateTime GetTimeScale(Scale scale)
        {
            var dt = DateTime.Now;

            switch (scale)
            {
                case Scale.All:
                    return dt;

                case Scale.Millisecond:
                    return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, dt.Millisecond);

                case Scale.Second:
                    return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second);

                case Scale.Minute:
                    return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, 0);

                case Scale.Hour:
                    return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, 0, 0);

                case Scale.Day:
                    return new DateTime(dt.Year, dt.Month, dt.Day, 0, 0, 0);

                case Scale.Month:
                    return new DateTime(dt.Year, dt.Month, 0, 0, 0, 0);

                case Scale.Year:
                    return new DateTime(dt.Year, 0, 0, 0, 0, 0);

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private DateTime GetScaledTime(Scale scale, DateTime dt, long duration = 0)
        {

            switch (scale)
            {
                case Scale.All:
                    return dt;

                case Scale.Millisecond:
                    return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, dt.Millisecond) - TimeSpan.FromMilliseconds(duration);

                case Scale.Second:
                    return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second) - TimeSpan.FromSeconds(duration);

                case Scale.Minute:
                    return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, 0) - TimeSpan.FromMinutes(duration);

                case Scale.Hour:
                    return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, 0, 0) - TimeSpan.FromHours(duration);

                case Scale.Day:
                    return DateTime.Today.AddDays(duration * -1);

                case Scale.Month:
                    return DateTime.Today.AddMonths((int)duration * -1);

                case Scale.Year:
                    return DateTime.Today.AddYears((int)duration * -1);

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private LinkedList<ActionPerTime> NormalizeList(Scale scale, long duration, LinkedList<ActionPerTime> itemList)
        {
            LinkedList<ActionPerTime> result = new LinkedList<ActionPerTime>();
            // Kleineste Datum in der Liste
            var startDate = GetScaledTime(scale, DateTime.Now, duration);

            // Größtes Datum in der Liste.
            var endDate = GetScaledTime(scale, DateTime.Now, 0);

            // von hinten nach vorne.
            if (itemList.Count == 0)
            {
                return GetEmptyList(scale, duration);
            }

            if (itemList.First != null && itemList.First.Value.Time > startDate)
            {
                itemList.AddFirst(new ActionPerTime() { Count = 0, Time = startDate });
            }

            using var enumerator = itemList.Reverse().GetEnumerator();
            enumerator.MoveNext();
            var item = enumerator.Current;
            var scaledItemTime = GetScaledTime(scale, item.Time);

            long idx = duration;
            var topItem = endDate;
            while (idx > 0)
            {
                if (scaledItemTime < topItem)
                {
                    result.AddFirst(new ActionPerTime() { Count = 0, Time = topItem });
                    topItem = DecrementScaledTime(scale, topItem);
                }
                else
                {
                    result.AddFirst(item);
                    topItem = DecrementScaledTime(scale, topItem);
                    if (enumerator.MoveNext())
                    {
                        item = enumerator.Current;
                        scaledItemTime = GetScaledTime(scale, item.Time);
                    }
                    else
                    {
                        break;
                    }
                }

                idx--;
            }

            return result;
        }

        private DateTime DecrementScaledTime(Scale scale, DateTime t)
        {
            switch (scale)
            {
                case Scale.All:
                    return t.AddMilliseconds(-1);
                case Scale.Millisecond:
                    return t.AddMilliseconds(-1);
                case Scale.Second:
                    return t.AddSeconds(-1);
                case Scale.Minute:
                    return t.AddMinutes(-1);
                case Scale.Hour:
                    return t.AddHours(-1);
                case Scale.Day:
                    return t.AddDays(-1);
                case Scale.Month:
                    return t.AddMonths(-1);
                case Scale.Year:
                    return t.AddYears(-1);
                default:
                    throw new ArgumentOutOfRangeException(nameof(scale), scale, null);
            }
        }

        private LinkedList<ActionPerTime> GetEmptyList(Scale scale, long duration)
        {
            if (duration == 0)
                return new LinkedList<ActionPerTime>();

            var now = GetScaledTime(scale, DateTime.Now);
            var from = GetScaledTime(scale, DateTime.Now, duration);
            var result = new LinkedList<ActionPerTime>();
            while (duration > 0)
            {
                result.AddLast(new ActionPerTime()
                {
                    Time = from,
                    Count = 0
                });
                duration -= 1;
                from = GetScaledTime(scale, DateTime.Now, duration);
            }

            return result;
        }

    }
}
