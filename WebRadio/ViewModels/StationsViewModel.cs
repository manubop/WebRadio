using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;

using Avalonia.Threading;

using Microsoft.Extensions.Logging;

using CommunityToolkit.Mvvm.Input;

using WebRadio.DataModel;
using WebRadio.Services;
using WebRadio.Utils;

namespace WebRadio.ViewModels
{
    public class StationsViewModel : ViewModelBase, IDisposable
    {
        private readonly IOptions _options;
        private readonly ILogger _logger;
        private readonly ISongDownloaderFactory _songDownloaderFactory;
        private readonly IStationEditor _stationEditor;

        private ISongInfoDownloader? _downloader;
        private IRadioStream? _stream;
        private IStationAppender? _appender;

        private float _volume = 1;

        public float Volume
        {
            get => _volume;
            set
            {
                if (SetProperty(ref _volume, value))
                {
                    _stream?.SetVolume(value);
                }
            }
        }

        private int _selectedIndex;

        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                if (SetProperty(ref _selectedIndex, value))
                {
                    MoveSelectedItemUpCommand.NotifyCanExecuteChanged();
                    MoveSelectedItemDownCommand.NotifyCanExecuteChanged();
                    RemoveSelectedItemCommand.NotifyCanExecuteChanged();
                    EditSelectedItemCommand.NotifyCanExecuteChanged();
                    PlaySelectedItemCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private bool _isItemPlaying;

        public bool IsItemPlaying
        {
            get => _isItemPlaying;
            set
            {
                if (SetProperty(ref _isItemPlaying, value))
                {
                    StopItemCommand.NotifyCanExecuteChanged();
                    PlaySelectedItemCommand.NotifyCanExecuteChanged();
                    PlayLastPlayedItemCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private int _lastPlayedIndex;

        public int LastPlayedIndex
        {
            get => _lastPlayedIndex;
            set
            {
                if (SetProperty(ref _lastPlayedIndex, value))
                {
                    PlayLastPlayedItemCommand.NotifyCanExecuteChanged();
                    PlayPrevItemCommand.NotifyCanExecuteChanged();
                    PlayNextItemCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private SongInfo _songInfo = SongInfo.Empty;

        public SongInfo SongInfo
        {
            get => _songInfo;
            set
            {
                SetProperty(ref _songInfo, value);
            }
        }

        private bool _buffering;

        public bool Buffering
        {
            get => _buffering;
            set
            {
                SetProperty(ref _buffering, value);
            }
        }

        public ObservableCollection<StationModel> Model { get; }
        public StationModel SelectedStation => Model[SelectedIndex];
        public StationModel LastPlayedStation => Model[LastPlayedIndex];

        public IRelayCommand MoveSelectedItemUpCommand { get; }
        public IRelayCommand MoveSelectedItemDownCommand { get; }
        public IRelayCommand RemoveSelectedItemCommand { get; }
        public IRelayCommand EditSelectedItemCommand { get; }
        public IRelayCommand PlaySelectedItemCommand { get; }
        public IRelayCommand StopItemCommand { get; }
        public IRelayCommand PlayLastPlayedItemCommand { get; }
        public IRelayCommand PlayPrevItemCommand { get; }
        public IRelayCommand PlayNextItemCommand { get; }

        public StationsViewModel(IStationService service, IOptions options, ILoggerFactory loggerFactory, ISongDownloaderFactory songDownloaderFactory, IStationEditor stationEditor)
        {
            Model = [.. service.Load().Select(s => new StationModel(s))];

            Model.CollectionChanged += (_, _) =>
            {
                service.Store(Model);
            };

            _options = options;
            _logger = loggerFactory.CreateLogger<StationsViewModel>();
            _songDownloaderFactory = songDownloaderFactory;
            _stationEditor = stationEditor;

            MoveSelectedItemUpCommand = new RelayCommand(MoveSelectedItemUp, () => SelectedIndex > 0);
            MoveSelectedItemDownCommand = new RelayCommand(MoveSelectedItemDown, () => SelectedIndex < Model.Count - 1);
            RemoveSelectedItemCommand = new RelayCommand(RemoveSelectedItem, () => SelectedIndex >= 0);
            EditSelectedItemCommand = new RelayCommand(EditSelectedItem, () => SelectedIndex >= 0);
            PlaySelectedItemCommand = new RelayCommand(PlaySelectedItem, () => SelectedIndex >= 0 && (!IsItemPlaying || SelectedIndex != LastPlayedIndex));
            StopItemCommand = new RelayCommand(StopItem, () => IsItemPlaying);
            PlayLastPlayedItemCommand = new RelayCommand(PlayLastPlayedItem, () => !IsItemPlaying);
            PlayPrevItemCommand = new RelayCommand(PlayPrevItem, () => LastPlayedIndex > 0);
            PlayNextItemCommand = new RelayCommand(PlayNextItem, () => LastPlayedIndex < Model.Count - 1);
        }

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
            _stationEditor.RemoveItem(SelectedIndex, "Delete selected item ?");
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

        public void MoveSelectedItemUp()
        {
            var selectedIndex = SelectedIndex;
            if (selectedIndex > 0)
            {
                Model.Move(selectedIndex, selectedIndex - 1);
                SelectedIndex = selectedIndex - 1;
            }
        }

        public void MoveSelectedItemDown()
        {
            var selectedIndex = SelectedIndex;
            if (selectedIndex < Model.Count - 1)
            {
                Model.Move(selectedIndex, selectedIndex + 1);
                SelectedIndex = selectedIndex + 1;
            }
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
