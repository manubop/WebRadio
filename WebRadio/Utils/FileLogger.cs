using System;
using System.IO;

using Microsoft.Extensions.Logging;

namespace WebRadio.Utils
{
    public class FileLoggerProvider(string filename) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, filename);

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }

    public class FileLogger(string categoryName, string filename) : ILogger
    {
        IDisposable? ILogger.BeginScope<TState>(TState state) => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            using var writer = new StreamWriter(filename, true);

            var message = formatter(state, exception);

            writer.WriteLine($"[{DateTime.Now}][{logLevel}][{categoryName}] {message}");
        }
    }

    public static class LoggingBuilderExt
    {
        public static ILoggingBuilder AddFileLoggerProvider(this ILoggingBuilder builder, string filename) => builder.AddProvider(new FileLoggerProvider(filename));
    }
}
