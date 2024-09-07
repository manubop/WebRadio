using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using Avalonia.Threading;

using Microsoft.Extensions.Logging;

using ReactiveUI;

using Un4seen.Bass;
using Un4seen.Bass.AddOn.Tags;

using WebRadio.DataModel;
using WebRadio.Services;
using WebRadio.Utils;

namespace WebRadio.ViewModels
{
    public class Options
    {
        public bool ShowDownloadInfo { get; set; } = true;
        public bool ShowICYTags { get; set; } = true;
    }

    public class StationsViewModel : ViewModelBase, IDisposable
    {
        private readonly Options _options;
        private readonly ILogger _logger;
        private readonly ISongDownloaderFactory _songDownloaderFactory;

        private ISongInfoDownloader? _downloader;

        IStream? _stream;
        System.Timers.Timer? _timer;
        DateTime _start;

        public StationsViewModel(IStationService service, Options options, ILoggerFactory loggerFactory, ISongDownloaderFactory songDownloaderFactory)
        {
            Model = new(service.Load().Select(s => new StationModel(s)));

            Model.CollectionChanged += (_, _) =>
            {
                service.Store(Model);
            };

            this.WhenAnyValue(x => x.Volume).Subscribe(volume => _stream?.SetAttribute(BASSAttribute.BASS_ATTRIB_VOL, volume));

            _options = options;
            _logger = loggerFactory.CreateLogger<StationsViewModel>();
            _songDownloaderFactory = songDownloaderFactory;
        }

        public ObservableCollection<StationModel> Model { get; }

        float _volume = 1;

        public float Volume
        {
            get => _volume;
            set
            {
                this.RaiseAndSetIfChanged(ref _volume, value);
            }
        }

        int _playingIndex = -1;

        public int PlayingIndex
        {
            get => _playingIndex;
            set
            {
                this.RaiseAndSetIfChanged(ref _playingIndex, value);
            }
        }

        public StationModel SelectedStation => Model[SelectedIndex];

        int _selectedIndex;

        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedIndex, value);
                this.RaisePropertyChanged(nameof(IsItemSelected));
            }
        }

        public bool IsItemSelected => _selectedIndex >= 0;

        bool _isItemPlaying;

        public bool IsItemPlaying
        {
            get => _isItemPlaying;
            set
            {
                this.RaiseAndSetIfChanged(ref _isItemPlaying, value);
            }
        }

        private SongInfo _songInfo = SongInfo.Empty;

        public SongInfo SongInfo
        {
            get => _songInfo;
            set
            {
                this.RaiseAndSetIfChanged(ref _songInfo, value);
            }
        }

        private bool _buffering;

        public bool Buffering
        {
            get => _buffering;
            set
            {
                this.RaiseAndSetIfChanged(ref _buffering, value);
            }
        }

        private void UpdateSongInfoFromTagInfo(TAG_INFO tagInfo)
        {
            if (SongInfo.Equals(tagInfo))
            {
                return;
            }

            Dispatcher.UIThread.Post(() =>
            {
                _logger.LogInformation("New TAG_INFO: {Artist} / {Title}", tagInfo.artist, tagInfo.title);

                SongInfo = new SongInfo
                {
                    Artist = tagInfo.artist,
                    Title = tagInfo.title
                };
            });
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes >= 0x40000000)
            {
                return ((double)(bytes >> 20) / 1024).ToString("0.00 GB", CultureInfo.InvariantCulture);
            }

            if (bytes >= 0x100000)
            {
                return ((double)(bytes >> 10) / 1024).ToString("0.00 MB", CultureInfo.InvariantCulture);
            }

            return ((double)bytes / 1024).ToString("0.00 KB", CultureInfo.InvariantCulture);
        }

        public void PlayItem()
        {
            _logger.LogInformation("PlayItem");

            if (Buffering)
            {
                return;
            }

            StopItem();

            var station = Model[SelectedIndex];

            station.Append = $"\u25B6 Buffering...";

            Buffering = true;
            PlayingIndex = SelectedIndex;

            Task.Run(() =>
            {
                _stream = CreateStream(station.Url, _options);

                Buffering = false;

                if (_stream == null)
                {
                    _logger.LogError("Failed to create stream !");

                    station.Append = string.Empty;

                    return;
                }

                if (!string.IsNullOrEmpty(station.Api))
                {
                    _downloader = _songDownloaderFactory.GetDownloader(station.Api);

                    if (_downloader != null)
                    {
                        _downloader.SongInfo += (_, args) =>
                        {
                            Dispatcher.UIThread.Post(() =>
                            {
                                SongInfo = new SongInfo { Artist = args.Artist, Title = args.Title };
                            });
                        };

                        _downloader.Start();
                    }
                    else
                    {
                        _logger.LogWarning("Could not find a suitable downloader: {Api}", station.Api);
                    }
                }
                else
                {
                    _stream.SetupTagDisplay(station.Url, UpdateSongInfoFromTagInfo);
                }

                IsItemPlaying = true;

                _stream.SetAttribute(BASSAttribute.BASS_ATTRIB_VOL, Volume);
                _stream.Play(true);

                _start = DateTime.Now;
                _timer = new System.Timers.Timer(100);

                _timer.Elapsed += (_, _) =>
                {
                    var diff = DateTime.Now - _start;
                    var pos = _stream.GetFilePosition(BASSStreamFilePosition.BASS_FILEPOS_DOWNLOAD);

                    station.Append = $"\u25B6 {diff.Hours:D2}:{diff.Minutes:D2}:{diff.Seconds:D2} / {FormatBytes(pos)}";
                };

                _timer.AutoReset = true;
                _timer.Enabled = true;
            });
        }

        public void StopItem()
        {
            if (_stream != null)
            {
                _stream.Dispose();
                _stream = null;

                _timer?.Dispose();
                _timer = null;

                _downloader?.Dispose();
                _downloader = null;

                var station = Model[PlayingIndex];

                station.Append = string.Empty;

                IsItemPlaying = false;

                SongInfo = SongInfo.Empty;

                PlayingIndex = -1;
            }
        }

        public void PlayPauseItem()
        {
            if (IsItemPlaying)
            {
                StopItem();
            }
            else
            {
                PlayItem();
            }
        }

        public void PlayPrevItem()
        {
            if (SelectedIndex > 0)
            {
                SelectedIndex--;
                PlayItem();
            }
        }

        public void PlayNextItem()
        {
            if (SelectedIndex < Model.Count - 1)
            {
                SelectedIndex++;
                PlayItem();
            }
        }

        private IStream? CreateStream(string url, Options options)
        {
            _logger.LogDebug("Opening {Url}", url);

            var stream = Stream.Create(url, BASSFlag.BASS_STREAM_STATUS, (IntPtr buffer, int length, IntPtr user) =>
            {
                if (buffer != IntPtr.Zero && length == 0 && options.ShowDownloadInfo)
                {
                    var txt = Marshal.PtrToStringAnsi(buffer);

                    _logger.LogDebug("Download info: {Info}", txt);
                }
            });

            if (stream == null)
            {
                _logger.LogError("Could not create stream from {Url}", url);
                return null;
            }

            var channelInfo = stream.GetInfo();

            _logger.LogDebug("Channel info: {ChannelInfo}", channelInfo);

            if (channelInfo.ctype == BASSChannelType.BASS_CTYPE_STREAM_MF)
            {
                var wftext = stream.GetTagsWAVEFORMAT();

                if (wftext != null)
                {
                    _logger.LogDebug("Sample rate: {SampleRate}kbps", wftext.waveformatex.nAvgBytesPerSec * 8 / 1000);
                }
            }

            if (options.ShowICYTags)
            {
                var icy = stream.GetTagsICY() ?? stream.GetTagsHTTP() ?? [];

                foreach (var tag in icy)
                {
                    if (tag.StartsWith("icy-metaint:", StringComparison.InvariantCultureIgnoreCase))
                    {
                        stream.HasMetadata = true;
                    }

                    _logger.LogDebug("ICY tag: {Tag}", tag);
                }
            }

            return stream;
        }

        public void Dispose()
        {
            _stream?.Dispose();
            _timer?.Dispose();
            _downloader?.Dispose();

            GC.SuppressFinalize(this);
        }
    }
}
