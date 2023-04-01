using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace Persistance.Sqlite
{
    readonly struct NullLogger<T> : ILogger<T>
    {
        readonly struct NullDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return false;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return new NullDisposable();
        }
    }
}
