using System;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

namespace WebRadio.Utils
{
    public class SongInfoEventArgs : EventArgs
    {
        public string Artist { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
    }

    public interface ISongInfoDownloader : IDisposable
    {
        public void Start();

        public event EventHandler<SongInfoEventArgs>? SongInfo;
    }

    internal abstract class SongInfoDownloaderBase<T> : ISongInfoDownloader
    {
        private readonly string _url;
        private readonly ILogger _logger;

        private readonly HttpClient _client = new();
        private readonly System.Timers.Timer _timer = new() { AutoReset = false };

        public event EventHandler<SongInfoEventArgs>? SongInfo;

        protected SongInfoDownloaderBase(string url, ILogger logger)
        {
            _url = url;
            _logger = logger;

            _timer.Elapsed += (_, _) =>
            {
                Start();
            };
        }

        public async void Start()
        {
            var info = await Update();

            if (info != null)
            {
                var interval = GetNextInterval(info);

                _logger.LogInformation("Delay to refresh: {DelatyToRefresh}", interval);

                _timer.Interval = interval;

                _timer.Start();
            }
        }

        protected abstract int GetNextInterval(T info);

        protected void RaiseSongInfoEvent(string artist, string title)
        {
            _logger.LogInformation("New song info: {Artist} / {Title}", artist, title);

            SongInfo?.Invoke(this, new SongInfoEventArgs { Artist = artist, Title = title });
        }

        protected void LogInformation(DateTime start, DateTime now, TimeSpan elapsed, TimeSpan duration)
        {
            _logger.LogInformation("Start: {Start} / Now: {Now} / elapsed: {Elapsed} / duration: {Duration}", start.ToLongTimeString(), now.ToLongTimeString(), elapsed.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture), duration);
        }

        private async Task<T?> Update()
        {
            using HttpResponseMessage response = await _client.GetAsync(_url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Error downloading radio metadata: {StatusCode}", response.StatusCode);

                return default;
            }

            var content = await response.Content.ReadAsStreamAsync();

            try
            {
                return await JsonSerializer.DeserializeAsync<T>(content);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error deserializing radio metadata: {Message}", ex.Message);

                return default;
            }
        }

        public void Dispose()
        {
            _timer.Dispose();
            _client.Dispose();

            GC.SuppressFinalize(this);
        }
    }

    internal sealed class FIPSongInfo
    {
        [JsonPropertyName("firstLine")]
        public string FirstLine { get; set; } = string.Empty;

        [JsonPropertyName("secondLine")]
        public string SecondLine { get; set; } = string.Empty;
    }

    internal sealed class FIPSongPayload
    {
        [JsonPropertyName("now")]
        public FIPSongInfo? Now { get; set; }

        [JsonPropertyName("next")]
        public FIPSongInfo? Next { get; set; }

        [JsonPropertyName("delayToRefresh")]
        public int DelayToRefresh { get; set; }
    }

    internal sealed class FIPSongInfoDownloader(string url, ILoggerFactory loggerFactory) : SongInfoDownloaderBase<FIPSongPayload>(url, loggerFactory.CreateLogger<FIPSongInfoDownloader>())
    {
        protected override int GetNextInterval(FIPSongPayload info)
        {
            var now = info.Now;

            if (now != null)
            {
                RaiseSongInfoEvent(now.SecondLine, now.FirstLine);
            }

            return info.DelayToRefresh;
        }
    }

    internal sealed class NOVASongInfo
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("artist")]
        public string Artist { get; set; } = string.Empty;

        [JsonPropertyName("diffusion_date")]
        public string DiffusionDate { get; set; } = string.Empty;

        [JsonPropertyName("duration")]
        public string Duration { get; set; } = string.Empty;
    }

    internal sealed class NOVASongPayload
    {
        [JsonPropertyName("currentTrack")]
        public NOVASongInfo? CurrentTrack { get; set; }
    }

    internal sealed class NOVASongInfoDownloader(string url, ILoggerFactory loggerFactory) : SongInfoDownloaderBase<NOVASongPayload>(url, loggerFactory.CreateLogger<NOVASongInfoDownloader>())
    {
        protected override int GetNextInterval(NOVASongPayload info)
        {
            var interval = 30000;
            var currentTrack = info.CurrentTrack;

            if (currentTrack != null)
            {
                RaiseSongInfoEvent(currentTrack.Artist, currentTrack.Title);

                var start = DateTime.Parse(currentTrack.DiffusionDate, CultureInfo.InvariantCulture);
                var now = DateTime.Now;
                var elapsed = now > start ? now - start : TimeSpan.Zero;

                if (TimeSpan.TryParse("00:" + currentTrack.Duration, out TimeSpan duration))
                {
                    LogInformation(start, now, elapsed, duration);

                    if (duration > elapsed)
                    {
                        interval = (int)(duration - elapsed).TotalMilliseconds;
                    }
                }
            }

            return interval;
        }
    }

    public interface ISongDownloaderFactory
    {
        ISongInfoDownloader? GetDownloader(string url);
    }

    public class SongDownloaderFactory(ILoggerFactory loggerFactory) : ISongDownloaderFactory
    {
        public ISongInfoDownloader? GetDownloader(string url)
        {
            if (url.Contains("/fip/"))
            {
                return new FIPSongInfoDownloader(url, loggerFactory);
            }

            if (url.Contains("/nova"))
            {
                return new NOVASongInfoDownloader(url, loggerFactory);
            }

            return null;
        }
    }
}
