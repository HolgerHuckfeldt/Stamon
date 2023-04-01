using Microsoft.Extensions.DependencyInjection;
using Persistance.Sqlite.Services;
using Stamon.Types.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace Persistance.Sqlite
{

    public static class SqliteProviderServiceCollectionExtensions
    {
        public static IServiceCollection AddSqliteProvider(this IServiceCollection services,
            Action<SqliteCacheOptions> setupAction)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (setupAction == null)
            {
                throw new ArgumentNullException(nameof(setupAction));
            }

            services.AddOptions();
            services.AddSingleton<IStamonDatabasePersistance, SqliteService>();
            services.AddSingleton<ItemCacheService>();

            services.Configure(setupAction);
            return services;
        }

        /// <summary>
        /// Registers <c>SqliteCache</c> as a dependency-injected singleton, available
        /// both as <c>IDistributedCache</c> and <c>SqliteCache</c>.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="path">The path where the SQLite database should be stored. It
        /// is created if it does not exist. (The path should be a file path, not a
        /// directory. Make sure the application has RW access at runtime.)</param>
        /// <returns></returns>
        public static IServiceCollection AddSqliteProvider(this IServiceCollection services,
            string path)
        {
            return AddSqliteProvider(services, options => options.CachePath = path);
        }
    }
}
