using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
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
        private readonly IStationEditor _stationEditor;

        private ISongInfoDownloader? _downloader;

        IStream? _stream;
        System.Timers.Timer? _timer;
        DateTime _start;

        public StationsViewModel(IStationService service, Options options, ILoggerFactory loggerFactory, ISongDownloaderFactory songDownloaderFactory, IStationEditor stationEditor)
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
            _stationEditor = stationEditor;
        }

        public ObservableCollection<StationModel> Model { get; }

        public void AddNewItem()
        {
            _stationEditor.AddItem();
        }

        public void EditSelectedItem()
        {
            _stationEditor.EditItem(SelectedStation);
        }

        public void RemoveSelectedItem()
        {
            Model.RemoveAt(SelectedIndex);
        }

        float _volume = 1;

        public float Volume
        {
            get => _volume;
            set
            {
                this.RaiseAndSetIfChanged(ref _volume, value);
            }
        }

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

        public StationModel SelectedStation => Model[_selectedIndex];

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

        public int LastPlayedIndex { get; set; }

        public StationModel LastPlayedStation => Model[LastPlayedIndex];

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

        public void PlayItem(int index)
        {
            _logger.LogInformation("PlayItem");

            if (Buffering)
            {
                return;
            }

            StopItem();

            var station = Model[index];

            station.Append = $"\u25B6 Buffering...";

            Buffering = true;
            LastPlayedIndex = index;

            Task.Run(() =>
            {
                _stream = StreamHelper.CreateStream(station.Url, _options, _logger);

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
                            SongInfo = new SongInfo { Artist = args.Artist, Title = args.Title };
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

                    station.Append = $"\u25B6 {diff.Hours:D2}:{diff.Minutes:D2}:{diff.Seconds:D2} / {StreamHelper.FormatBytes(pos)}";
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

                var station = Model[LastPlayedIndex];

                station.Append = string.Empty;

                IsItemPlaying = false;

                SongInfo = SongInfo.Empty;
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
                PlayItem(LastPlayedIndex);
            }
        }

        public void PlayPrevItem()
        {
            if (LastPlayedIndex > 0)
            {
                PlayItem(LastPlayedIndex - 1);
            }
        }

        public void PlayNextItem()
        {
            if (LastPlayedIndex < Model.Count - 1)
            {
                PlayItem(LastPlayedIndex + 1);
            }
        }

        public void PlayLastPlayedItem()
        {
            PlayItem(LastPlayedIndex);
        }

        public void PlaySelectedItem()
        {
            PlayItem(SelectedIndex);
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
