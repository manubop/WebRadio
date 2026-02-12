using System;
using System.Timers;
using WebRadio.DataModel;

namespace WebRadio.Utils
{
    internal interface IStationAppender : IDisposable
    {
        void Stop();
    }

    internal sealed class StationAppender(StationModel station, IRadioStream stream) : IStationAppender
    {
        private readonly Timer _timer = new() { AutoReset = false };

        private void Reset()
        {
            _timer.Interval = 1000 - DateTime.Now.Millisecond;
            _timer.Start();
        }

        public void Start()
        {
            var start = DateTime.Now;

            _timer.Elapsed += (_, _) =>
            {
                var diff = DateTime.Now - start;
                var pos = stream.GetDownloadFilePosition();

                station.Append = $"\u25B6 {diff.Hours:D2}:{diff.Minutes:D2}:{diff.Seconds:D2} / {StreamHelper.FormatBytes(pos)}";

                Reset();
            };

            Reset();
        }

        public void Stop()
        {
            _timer.Stop();

            station.Append = string.Empty;
        }

        public void Dispose()
        {
            _timer.Dispose();

            GC.SuppressFinalize(this);
        }
    }

    internal static class StationAppenderFactory
    {
        public static IStationAppender? Create(StationModel station, IRadioStream stream)
        {
            if (station == null || stream == null)
            {
                return null;
            }

            var result = new StationAppender(station, stream);

            result.Start();

            return result;
        }
    }
}
