using System;
using System.IO;
using System.Reactive.Linq;
using System.Text.Json;

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
    public class AppState
    {
        public int SelectedIndex { get; set; }

        public bool IsPlaying { get; set; }

        public void Save(string filename)
        {
            var text = JsonSerializer.Serialize(this);

            File.WriteAllText(filename, text);
        }

        public static AppState? Load(string filename)
        {
            try
            {
                var text = File.ReadAllText(filename);

                return JsonSerializer.Deserialize<AppState>(text);
            }
            catch (Exception ex)
            {
                App.logger.LogWarning("Could not read state: {Exception}", ex.Message);

                return null;
            }
        }
    }

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

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var service = new StationsService(stationsFilename);
                var options = new Options();
                var songDownloaderFactory = new SongDownloaderFactory(loggerFactory);
                var vm = new MainWindowViewModel(service, options, loggerFactory, songDownloaderFactory);
                var mw = new MainWindow
                {
                    DataContext = vm,
                };

                vm.Stations.WhenAnyValue(x => x.SongInfo, x => x.PlayingIndex).Subscribe(x =>
                {
                    if (x.Item2 != -1)
                    {
                        var songInfo = x.Item1;

                        mw.Title = songInfo.IsEmpty() ? vm.Stations.SelectedStation.Name : songInfo.Artist + " / " + songInfo.Title;
                    }
                    else
                    {
                        mw.Title = "WebRadio";
                    }
                });

                desktop.MainWindow = mw;

                RegisterTrayIcon(vm.Stations);

                desktop.Startup += OnStartup;
                desktop.Exit += OnExit;

                var hotKeyManager = new GlobalHotKeys.HotKeyManager();
                var hotKeys = new[]
                {
                    hotKeyManager.Register(VirtualKeyCode.VK_MEDIA_PLAY_PAUSE, 0),
                    hotKeyManager.Register(VirtualKeyCode.VK_MEDIA_PREV_TRACK, 0),
                    hotKeyManager.Register(VirtualKeyCode.VK_MEDIA_NEXT_TRACK, 0),
                };

                var context = AvaloniaSynchronizationContext.Current;

                if (context != null)
                {
                    hotKeyManager.HotKeyPressed
                      .ObserveOn(context)
                      .Subscribe(hotKey =>
                      {
                          switch (hotKey.Key)
                          {
                              case VirtualKeyCode.VK_MEDIA_PLAY_PAUSE:
                                  vm.Stations.PlayPauseItem();
                                  break;
                              case VirtualKeyCode.VK_MEDIA_PREV_TRACK:
                                  vm.Stations.PlayPrevItem();
                                  break;
                              case VirtualKeyCode.VK_MEDIA_NEXT_TRACK:
                                  vm.Stations.PlayNextItem();
                                  break;
                          }
                      });
                }

                desktop.Exit += (sender, args) =>
                {
                    foreach (var hotKey in hotKeys)
                    {
                        hotKey.Dispose();
                    }
                    hotKeyManager.Dispose();
                };
            }

            base.OnFrameworkInitializationCompleted();
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

                    if (appState.IsPlaying)
                    {
                        vm.Stations.PlayItem();
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
                    IsPlaying = vm.Stations.PlayingIndex != -1,
                };

                appState.Save(stateFilename);
            }

            Bass.BASS_Stop();
            Bass.BASS_Free();
        }
    }
}
