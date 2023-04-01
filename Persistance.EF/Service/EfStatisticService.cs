using Persistance.EF.Database;
using Microsoft.EntityFrameworkCore;
using Stamon.Types.Interfaces;
using Stamon.Types.Model;

namespace Persistance.EF.Service
{
    public class EfStatisticService : IStamonDatabasePersistance
    {

        public static string? DbPath;


        public EfStatisticService(string dbPath)
        {
            DbPath = dbPath;

            try
            {
                using var db = new StamonContext();
                db.Database.Migrate();
            }
            catch (Exception ex)
            {

            }
        }

        public async Task AddMonitor(Stamon.Types.Model.Monitor monitor)
        {
            await using var db = new Database.StamonContext();

            var entry = await db.Slots.FirstOrDefaultAsync(s => s.Name == monitor.Name && s.Scale == monitor.Scale);

            var scaledTime = GetScaledTime(monitor.Scale, DateTime.Now);

            if (entry != null)
            {
                var lastStatEntry = await db.Items.OrderByDescending(s => s.Time).FirstOrDefaultAsync(s => s.SlotId == entry.Id);
                if (lastStatEntry != null)
                {
                    if (lastStatEntry.Time == scaledTime)
                    {
                        lastStatEntry.Count += 1;
                    }
                    else
                    {
                        var test = GetScaledTime(monitor.Scale, DateTime.Now);
                        db.Items.Add(new Database.StatisticItem()
                        {
                            Slot = entry,
                            SlotId = entry.Id,
                            Time = GetScaledTime(monitor.Scale, DateTime.Now),
                            Count = 1
                        });
                    }
                }
            }
            else
            {
                var newSlot = new Slot
                {
                    Name = monitor.Name,
                    Scale = monitor.Scale,
                    Created = DateTime.Now,
                    Duration = monitor.Duration,
                    Items = new List<StatisticItem>()
                };

                newSlot.Items.Add(new Database.StatisticItem()
                {
                    Time = GetScaledTime(monitor.Scale, DateTime.Now),
                    Count = 1
                }); 

                db.Slots.Add(newSlot);

                await db.SaveChangesAsync();
            }

            await db.SaveChangesAsync();
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
            await using var db = new Database.StamonContext();

            // Get the slot with that name
            var dbSlot = await db.Slots.FirstOrDefaultAsync(s => s.Name == monitor.Name && s.Scale == monitor.Scale);

            if (dbSlot != default)
            {
                var items = db.Items.Where(s => s.SlotId == dbSlot.Id && s.Time >= from && s.Time <= to);

                var result = new LinkedList<ActionPerTime>();
                foreach (var item in items)
                {
                    result.AddLast(new ActionPerTime() { Count = item.Count, Time = item.Time });
                }

                return NormalizeList(monitor.Scale, monitor.Duration, result);
            }
            return GetEmptyList(monitor.Scale, monitor.Duration); ;
        }

        public async Task<ICollection<string>> GetMonitorNames()
        {
            await using var db = new Database.StamonContext();
            var names = await db.Slots.Select(s =>s.Name).Distinct().ToListAsync();
            return names;
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

        private int ScaledTimeDuration(Scale scale, DateTime a, DateTime b)
        {
            var start = GetScaledTime(scale, a);
            var end = GetScaledTime(scale, b);

            switch (scale)
            {
                case Scale.All:
                    return (int)(end - start).TotalMilliseconds;
                case Scale.Millisecond:
                    return (int)(end - start).TotalMilliseconds;
                case Scale.Second:
                    return (int)(end - start).TotalSeconds;
                case Scale.Minute:
                    return (int)(end - start).TotalMinutes;
                case Scale.Hour:
                    return (int)(end - start).TotalHours;
                case Scale.Day:
                    return (int)(end - start).TotalDays;
                case Scale.Month:
                    {
                        int monthsApart = 12 * (start.Year - end.Year) + start.Month - end.Month;
                        return Math.Abs(monthsApart);
                    }
                case Scale.Year:
                {
                    int yearsApart = end.Year - start.Year;
                    return Math.Abs(yearsApart);
                    }
                default:
                    throw new ArgumentOutOfRangeException(nameof(scale), scale, null);
            }
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

        private DateTime IncrementScaledTime(Scale scale, DateTime t)
        {
            switch (scale)
            {
                case Scale.All:
                    return t.AddMilliseconds(1);
                case Scale.Millisecond:
                    return t.AddMilliseconds(1);
                case Scale.Second:
                    return t.AddSeconds(1);
                case Scale.Minute:
                    return t.AddMinutes(1);
                case Scale.Hour:
                    return t.AddHours(1);
                case Scale.Day:
                    return t.AddDays(1);
                case Scale.Month:
                    return t.AddMonths(1);
                case Scale.Year:
                    return t.AddYears(1);
                default:
                    throw new ArgumentOutOfRangeException(nameof(scale), scale, null);
            }
        }

        private string GetKey(string name, Scale scale)
        {
            return $"{name}_{scale}";
        }


    }
}
