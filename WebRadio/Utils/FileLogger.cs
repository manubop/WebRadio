using System;
using System.IO;

using Microsoft.Extensions.Logging;

namespace WebRadio.Utils
{
    public class FileLoggerProvider(string filename) : ILoggerProvider
    {
        private readonly StreamWriter _writer = new(filename, true) { AutoFlush = true };

        public ILogger CreateLogger(string categoryName)
        {
            return new FileLogger(categoryName, _writer);
        }

        public void Dispose()
        {
            _writer.Dispose();

            GC.SuppressFinalize(this);
        }
    }

    public class FileLogger(string categoryName, StreamWriter writer) : ILogger
    {
        IDisposable? ILogger.BeginScope<TState>(TState state)
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);

            writer.WriteLine($"[{DateTime.Now}][{logLevel}][{categoryName}] {message}");
        }
    }

    public static class LoggingBuilderExt
    {
        public static ILoggingBuilder AddFileLoggerProvider(this ILoggingBuilder builder, string filename)
        {
            return builder.AddProvider(new FileLoggerProvider(filename));
        }
    }
}
