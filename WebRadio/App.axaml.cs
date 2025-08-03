using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

using GlobalHotKeys.Native.Types;

using Microsoft.Extensions.Logging;
using Microsoft.Toolkit.Uwp.Notifications;

using ReactiveUI;

using Un4seen.Bass;

using WebRadio.Services;
using WebRadio.Utils;
using WebRadio.ViewModels;
using WebRadio.Views;

namespace WebRadio
{
    public partial class App : Application
    {
        private static readonly string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        private static readonly string stationsFilename = Path.Combine(homeDir, ".radio-console", "stations.json");
        private static readonly string loggerFilename = Path.Combine(homeDir, ".radio-console", "log.txt");
        private static readonly string stateFilename = Path.Combine(homeDir, ".radio-console", "state.json");

        public static readonly ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug).AddFileLoggerProvider(loggerFilename));
        public static readonly ILogger logger = loggerFactory.CreateLogger<App>();

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (!Design.IsDesignMode)
            {
                logger.LogInformation("Initializing !");
            }

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
            {
                var service = new StationsService(stationsFilename);
                var options = new Options();
                var songDownloaderFactory = new SongDownloaderFactory(loggerFactory);
                var vm = new MainWindowViewModel(service, options, loggerFactory, songDownloaderFactory);
                var mw = new MainWindow
                {
                    DataContext = vm,
                };

                vm.Stations.WhenAnyValue(x => x.SongInfo, x => x.IsItemPlaying).Subscribe(x =>
                {
                    if (x.Item2)
                    {
                        var songInfo = x.Item1;

                        mw.Title = songInfo.IsEmpty() ? vm.Stations.LastPlayedStation.Name : songInfo.Artist + " / " + songInfo.Title;

                        if (!songInfo.IsEmpty())
                        {
                            new ToastContentBuilder()
                                .AddArgument("artist", songInfo.Artist)
                                .AddArgument("track", songInfo.Title)
                                .AddText(songInfo.Artist + " / " + songInfo.Title)
                                .AddText(vm.Stations.LastPlayedStation.Name)
                                .AddAudio(new ToastAudio { Silent = true })
                                .Show();
                        }
                    }
                    else
                    {
                        mw.Title = "WebRadio";
                    }
                });

                var topLevel = TopLevel.GetTopLevel(mw);

                if (topLevel != null)
                {
                    var launcher = topLevel.Launcher;

                    if (launcher != null)
                    {
                        ToastNotificationManagerCompat.OnActivated += argsCompat =>
                        {
                            var args = ToastArguments.Parse(argsCompat.Argument);
                            var track = args.Get("track");
                            var artist = args.Get("artist");

                            launcher.LaunchUriAsync(new Uri($"https://www.discogs.com/search/?type=all&track={track}&artist={artist}"));
                        };
                    }

                    SetupShortcuts(topLevel, vm.Stations, mw, lifetime);
                }

                lifetime.MainWindow = mw;

                RegisterTrayIcon(vm.Stations);

                lifetime.Startup += OnStartup;
                lifetime.Exit += OnExit;

                SetupHotKeys(lifetime, new Dictionary<VirtualKeyCode, Action> {
                    { VirtualKeyCode.VK_MEDIA_PLAY_PAUSE, () => vm.Stations.PlayPauseItem() },
                    { VirtualKeyCode.VK_MEDIA_PREV_TRACK, () => vm.Stations.PlayPrevItem() },
                    { VirtualKeyCode.VK_MEDIA_NEXT_TRACK, () => vm.Stations.PlayNextItem() },
                });
            }

            base.OnFrameworkInitializationCompleted();
        }

        private static void SetupHotKeys(IClassicDesktopStyleApplicationLifetime lifetime, IDictionary<VirtualKeyCode, Action> hotKeyActions)
        {
            var context = SynchronizationContext.Current;

            if (context == null)
            {
                return;
            }

            var hotKeyManager = new GlobalHotKeys.HotKeyManager();

            hotKeyManager.HotKeyPressed
                .ObserveOn(context)
                .Subscribe(hotKey => hotKeyActions[hotKey.Key].Invoke());

            var hotKeys = hotKeyActions.Keys.Select(key => hotKeyManager.Register(key, 0)).ToList();

            lifetime.Exit += (sender, args) =>
            {
                foreach (var hotKey in hotKeys)
                {
                    hotKey.Dispose();
                }

                hotKeyManager.Dispose();
            };
        }

        private static void SetupShortcuts(InputElement topLevel, StationsViewModel stations, Window mw, IControlledApplicationLifetime lifetime)
        {
            topLevel.KeyDown += (sender, args) =>
            {
                switch (args.Key)
                {
                    case Key.Up:
                        if (stations.SelectedIndex > 0)
                        {
                            stations.SelectedIndex--;
                        }
                        break;
                    case Key.Down:
                        if (stations.SelectedIndex < stations.Model.Count - 1)
                        {
                            stations.SelectedIndex++;
                        }
                        break;
                    case Key.PageUp:
                        {
                            var selectedIndex = stations.SelectedIndex;

                            if (selectedIndex > 0)
                            {
                                selectedIndex -= 5;

                                if (selectedIndex < 0)
                                {
                                    selectedIndex = 0;
                                }

                                stations.SelectedIndex = selectedIndex;
                            }
                        }
                        break;
                    case Key.PageDown:
                        {
                            var selectedIndex = stations.SelectedIndex;
                            var max = stations.Model.Count - 1;

                            if (selectedIndex < max)
                            {
                                selectedIndex += 5;

                                if (selectedIndex > max)
                                {
                                    selectedIndex = max;
                                }

                                stations.SelectedIndex = selectedIndex;
                            }
                        }
                        break;
                    case Key.Home:
                        if (stations.SelectedIndex > 0)
                        {
                            stations.SelectedIndex = 0;
                        }
                        break;
                    case Key.End:
                        if (stations.SelectedIndex < stations.Model.Count - 1)
                        {
                            stations.SelectedIndex = stations.Model.Count - 1;
                        }
                        break;
                    case Key.Enter:
                        stations.PlaySelectedItem();
                        break;
                    case Key.Back:
                        stations.StopItem();
                        break;
                    case Key.Escape:
                        mw.Hide();
                        mw.ShowInTaskbar = false;
                        break;
                    case Key.Subtract:
                        {
                            var vol = stations.Volume;

                            if (vol > 0f)
                            {
                                vol -= 0.1f;

                                if (vol < 0f)
                                {
                                    vol = 0f;
                                }

                                stations.Volume = vol;
                            }
                        }
                        break;
                    case Key.Add:
                        {
                            var vol = stations.Volume;

                            if (vol < 1f)
                            {
                                vol += 0.1f;

                                if (vol > 1f)
                                {
                                    vol = 1f;
                                }

                                stations.Volume = vol;
                            }
                        }
                        break;
                    case Key.Q:
                        if (args.KeyModifiers == KeyModifiers.Control)
                        {
                            lifetime.Shutdown();
                        }
                        break;
                }
            };
        }

        private void OnStartup(object? sender, ControlledApplicationLifetimeStartupEventArgs e)
        {
            var bassVersion = Bass.BASS_GetVersion();

            if (Un4seen.Bass.Utils.HighWord(bassVersion) != Bass.BASSVERSION)
            {
                logger.LogError("Unsupported BASS version: 0x{BassVersion:X8}", bassVersion);
                Environment.Exit(1);
            }

            logger.LogInformation("Loaded BASS version: 0x{BassVersion:X8}", bassVersion);

            Bass.BASS_SetConfig(BASSConfig.BASS_CONFIG_NET_PLAYLIST, 1);

            if (!Bass.BASS_Init(-1, 44100, BASSInit.BASS_DEVICE_DEFAULT, -1))
            {
                logger.LogError("Could not initialize BASS: {ErrorCode}", Bass.BASS_ErrorGetCode());
                Environment.Exit(1);
            }

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow?.DataContext is MainWindowViewModel vm)
            {
                var appState = AppState.Load(stateFilename);

                if (appState != null)
                {
                    vm.Stations.SelectedIndex = appState.SelectedIndex;
                    vm.Stations.LastPlayedIndex = appState.LastPlayedIndex;
                    vm.Stations.Volume = appState.Volume;

                    if (appState.IsItemPlaying)
                    {
                        vm.Stations.PlayItem(appState.LastPlayedIndex);
                    }
                }
            }
        }

        private void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
        {
            logger.LogInformation("Closing !");

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow?.DataContext is MainWindowViewModel vm)
            {
                var appState = new AppState
                {
                    SelectedIndex = vm.Stations.SelectedIndex,
                    IsItemPlaying = vm.Stations.IsItemPlaying,
                    LastPlayedIndex = vm.Stations.LastPlayedIndex,
                    Volume = vm.Stations.Volume,
                };

                appState.Save(stateFilename);
            }

            Bass.BASS_Stop();
            Bass.BASS_Free();
        }
    }
}
