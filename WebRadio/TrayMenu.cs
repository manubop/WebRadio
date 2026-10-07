using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.Input;

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
                Command = new RelayCommand(mainWindow.ToggleVisibility),
                Menu =
                [
                    new NativeMenuItem
                    {
                        Header = "Play",
                        Command = vm.PlayLastPlayedItemCommand
                    },
                    new NativeMenuItem
                    {
                        Header = "Stop",
                        Command = vm.StopItemCommand
                    },
                    new NativeMenuItem
                    {
                        Header = "Prev",
                        Command = vm.PlayPrevItemCommand,
                    },
                    new NativeMenuItem
                    {
                        Header = "Next",
                        Command = vm.PlayNextItemCommand
                    },
                    new NativeMenuItemSeparator(),
                    new NativeMenuItem
                    {
                        Header = "Exit",
                        Command = new RelayCommand(() => lifetime.Shutdown())
                    }
                ]
            };

            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName is nameof(StationsViewModel.SongInfo) or nameof(StationsViewModel.IsItemPlaying))
                {
                    UpdateTrayIconToolTip(vm, trayIcon);
                }
            };

            SetValue(TrayIcon.IconsProperty, [trayIcon]);
        }

        private static void UpdateTrayIconToolTip(StationsViewModel vm, TrayIcon trayIcon)
        {
            if (!vm.IsItemPlaying)
            {
                trayIcon.ToolTipText = "WebRadio";
                return;
            }

            var songInfo = vm.SongInfo;
            var prepend = string.Empty;

            if (!songInfo.IsEmpty())
            {
                prepend = songInfo.Artist
                    + " / "
                    + songInfo.Title
                    + Environment.NewLine;
            }

            trayIcon.ToolTipText = prepend + vm.LastPlayedStation.Name;
        }
    }
}
