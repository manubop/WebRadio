using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Timers;

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

    internal sealed class StationAppender(StationModel station, IRadioStream stream, int interval) : IDisposable
    {
        private readonly Timer _timer = new(interval) { AutoReset = true };

        public void Start()
        {
            var start = DateTime.Now;

            _timer.Elapsed += (_, _) =>
            {
                var diff = DateTime.Now - start;
                var pos = stream.GetFilePosition(BASSStreamFilePosition.BASS_FILEPOS_DOWNLOAD);

                station.Append = $"\u25B6 {diff.Hours:D2}:{diff.Minutes:D2}:{diff.Seconds:D2} / {StreamHelper.FormatBytes(pos)}";
            };

            _timer.Enabled = true;
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

    public class StationsViewModel : ViewModelBase, IDisposable
    {
        private readonly Options _options;
        private readonly ILogger _logger;
        private readonly ISongDownloaderFactory _songDownloaderFactory;
        private readonly IStationEditor _stationEditor;

        private ISongInfoDownloader? _downloader;
        private IRadioStream? _stream;
        private StationAppender? _appender;

        public StationsViewModel(IStationService service, Options options, ILoggerFactory loggerFactory, ISongDownloaderFactory songDownloaderFactory, IStationEditor stationEditor)
        {
            Model = [.. service.Load().Select(s => new StationModel(s))];

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

        private void UpdateSongInfoFromTagInfo(TAG_INFO tagInfo)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (SongInfo.Equals(tagInfo))
                {
                    return;
                }

                _logger.LogInformation("New TAG_INFO: {Artist} / {Title}", tagInfo.artist, tagInfo.title);

                SongInfo = new SongInfo
                {
                    Artist = tagInfo.artist,
                    Title = tagInfo.title
                };
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

            _appender = new StationAppender(station, _stream, 100);

            _appender.Start();
        }

        public void StopItem()
        {
            if (_stream != null)
            {
                _stream.Dispose();
                _stream = null;

                _appender?.Stop();
                _appender?.Dispose();
                _appender = null;

                _downloader?.Dispose();
                _downloader = null;

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
            _appender?.Dispose();
            _downloader?.Dispose();

            GC.SuppressFinalize(this);
        }
    }
}
