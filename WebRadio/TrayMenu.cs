using System;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ReactiveUI;

using WebRadio.ViewModels;
using WebRadio.Views;

namespace WebRadio
{
    public partial class App : Application
    {
        private void RegisterTrayIcon(StationsViewModel vm, MainWindow mainWindow, IControlledApplicationLifetime lifetime)
        {
            var trayIcon = new TrayIcon
            {
                IsVisible = true,
                ToolTipText = "WebRadio",
                Icon = new WindowIcon(new Bitmap(AssetLoader.Open(new Uri("avares://WebRadio/Assets/avalonia-logo.ico")))),
                Command = ReactiveCommand.Create(() => mainWindow.ToggleVisibility()),
                Menu =
                [
                    new NativeMenuItem
                    {
                        Header = "Play",
                        Command = ReactiveCommand.Create(() => vm.PlayLastPlayedItem(), vm.WhenAnyValue(x => x.IsItemPlaying, x => !x))
                    },
                    new NativeMenuItem
                    {
                        Header = "Stop",
                        Command = ReactiveCommand.Create(() => vm.StopItem(), vm.WhenAnyValue(x => x.IsItemPlaying))
                    },
                    new NativeMenuItem
                    {
                        Header = "Prev",
                        Command = ReactiveCommand.Create(() => vm.PlayPrevItem(), vm.WhenAnyValue(x => x.LastPlayedIndex, x => x > 0))
                    },
                    new NativeMenuItem
                    {
                        Header = "Next",
                        Command = ReactiveCommand.Create(() => vm.PlayNextItem(), vm.WhenAnyValue(x => x.LastPlayedIndex, x => x < vm.Model.Count - 1))
                    },
                    new NativeMenuItemSeparator(),
                    new NativeMenuItem
                    {
                        Header = "Exit",
                        Command = ReactiveCommand.Create(() => lifetime.Shutdown())
                    }
                ]
            };

            vm.WhenAnyValue(x => x.SongInfo, x => x.IsItemPlaying).Subscribe(x =>
            {
                if (x.Item2)
                {
                    var prepend = "";
                    var songInfo = x.Item1;

                    if (!songInfo.IsEmpty())
                    {
                        prepend = songInfo.Artist + " / " + songInfo.Title + Environment.NewLine;
                    }

                    trayIcon.ToolTipText = prepend + vm.LastPlayedStation.Name;
                }
                else
                {
                    trayIcon.ToolTipText = "WebRadio";
                }
            });

            SetValue(TrayIcon.IconsProperty, [trayIcon]);
        }
    }
}
