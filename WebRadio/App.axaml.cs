using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

using GlobalHotKeys.Native.Types;

using Microsoft.Extensions.Logging;

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
            logger.LogInformation("Initializing !");

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

                var context = SynchronizationContext.Current;

                if (context != null)
                {
                    vm.Stations.WhenAnyValue(x => x.SongInfo, x => x.IsItemPlaying).ObserveOn(context).Subscribe(x =>
                    {
                        if (x.Item2)
                        {
                            var songInfo = x.Item1;

                            mw.Title = songInfo.IsEmpty() ? vm.Stations.LastPlayedStation.Name : songInfo.Artist + " / " + songInfo.Title;

                        }
                        else
                        {
                            mw.Title = "WebRadio";
                        }
                    });
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
            var context = AvaloniaSynchronizationContext.Current;

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

        private void OnStartup(object? sender, ControlledApplicationLifetimeStartupEventArgs e)
        {
            if (Un4seen.Bass.Utils.HighWord(Bass.BASS_GetVersion()) != Bass.BASSVERSION)
            {
                logger.LogError("Wrong Bass Version!");
                Environment.Exit(1);
            }

            Bass.BASS_SetConfig(BASSConfig.BASS_CONFIG_NET_PLAYLIST, 1);

            if (!Bass.BASS_Init(-1, 44100, BASSInit.BASS_DEVICE_DEFAULT, -1))
            {
                logger.LogError("Could not initialize BASS");
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
