using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;

using Avalonia.Threading;

using Microsoft.Extensions.Logging;

using ReactiveUI;

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
        private IRadioStream? _stream;
        private IStationAppender? _appender;

        public StationsViewModel(IStationService service, Options options, ILoggerFactory loggerFactory, ISongDownloaderFactory songDownloaderFactory, IStationEditor stationEditor)
        {
            Model = [.. service.Load().Select(s => new StationModel(s))];

            Model.CollectionChanged += (_, _) =>
            {
                service.Store(Model);
            };

            this.WhenAnyValue(x => x.Volume).Subscribe(volume => _stream?.SetVolume(volume));

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

        public void About()
        {
            _stationEditor.About();
        }

        public void SelectorTop()
        {
            if (SelectedIndex > 0)
            {
                SelectedIndex = 0;
            }
        }

        public void SelectorBottom()
        {
            if (SelectedIndex < Model.Count - 1)
            {
                SelectedIndex = Model.Count - 1;
            }
        }

        public void SelectorUp(int count)
        {
            var selectedIndex = SelectedIndex;

            if (selectedIndex > 0)
            {
                selectedIndex -= count;

                if (selectedIndex < 0)
                {
                    selectedIndex = 0;
                }

                SelectedIndex = selectedIndex;
            }
        }

        public void SelectorDown(int count)
        {
            var selectedIndex = SelectedIndex;
            var max = Model.Count - 1;

            if (selectedIndex < max)
            {
                selectedIndex += count;

                if (selectedIndex > max)
                {
                    selectedIndex = max;
                }

                SelectedIndex = selectedIndex;
            }
        }

        public void VolumeUp(float count)
        {
            var vol = Volume;

            if (vol < 1f)
            {
                vol += count;

                if (vol > 1f)
                {
                    vol = 1f;
                }

                Volume = vol;
            }
        }

        public void VolumeDown(float count)
        {
            var vol = Volume;

            if (vol > 0f)
            {
                vol -= count;

                if (vol < 0f)
                {
                    vol = 0f;
                }

                Volume = vol;
            }
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

        int _lastPlayedIndex;

        public int LastPlayedIndex
        {
            get => _lastPlayedIndex;
            set
            {
                this.RaiseAndSetIfChanged(ref _lastPlayedIndex, value);
            }
        }

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

        private void UpdateSongInfoFromTagInfo(SongInfo songInfo)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (SongInfo.Equals(songInfo))
                {
                    return;
                }

                _logger.LogInformation("New TAG_INFO: {Artist} / {Title}", songInfo.Artist, songInfo.Title);

                SongInfo = songInfo;
            });
        }

        public async void PlayItem(int index)
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

            _stream = await StreamHelper.CreateStream(station.Url, _options, _logger);

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
                            SongInfo = new SongInfo(args.Artist, args.Title);
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

            _stream.SetVolume(Volume);
            _stream.Play(true);

            _appender = StationAppenderFactory.Create(station, _stream);
        }

        public void StopItem()
        {
            if (_stream != null)
            {
                _stream.Dispose();
                _stream = null;
            }

            if (_appender != null)
            {
                _appender.Stop();
                _appender.Dispose();
                _appender = null;
            }

            if (_downloader != null)
            {
                _downloader.Dispose();
                _downloader = null;
            }

            IsItemPlaying = false;

            SongInfo = SongInfo.Empty;
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
            _appender?.Dispose();
            _downloader?.Dispose();

            GC.SuppressFinalize(this);
        }
    }
}
