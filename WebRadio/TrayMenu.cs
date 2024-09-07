using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

using ReactiveUI;

using WebRadio.ViewModels;

namespace WebRadio
{
    public partial class App : Application
    {
        private void PlayCommand()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow?.DataContext is MainWindowViewModel vm)
            {
                vm.Stations.PlayItem();
            }
        }

        private void StopCommand()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow?.DataContext is MainWindowViewModel vm)
            {
                vm.Stations.StopItem();
            }
        }

        private void PrevCommand()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow?.DataContext is MainWindowViewModel vm && vm.Stations.SelectedIndex > 0)
            {
                vm.Stations.SelectedIndex--;
                vm.Stations.PlayItem();
            }
        }

        private void NextCommand()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow?.DataContext is MainWindowViewModel vm && vm.Stations.SelectedIndex < vm.Stations.Stations.Count - 1)
            {
                vm.Stations.SelectedIndex++;
                vm.Stations.PlayItem();
            }
        }

        private void ExitCommand()
        {
            if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
            {
                lifetime.Shutdown();
            }
        }

        private void ShowCommand()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mw = desktop.MainWindow;

                if (mw != null)
                {
                    if (mw.IsVisible)
                    {
                        mw.ShowInTaskbar = false;
                        mw.Hide();
                    }
                    else
                    {
                        mw.ShowInTaskbar = true;
                        mw.Show();
                    }
                }
            }
        }

        private void RegisterTrayIcon(StationsViewModel vm)
        {
            var trayIcon = new TrayIcon
            {
                IsVisible = true,
                ToolTipText = "WebRadio",
                Icon = new WindowIcon(new Bitmap(AssetLoader.Open(new Uri("avares://WebRadio/Assets/avalonia-logo.ico")))),
                Command = ReactiveCommand.Create(ShowCommand),
                Menu =
                [
                    new NativeMenuItem
                    {
                        Header = "Play",
                        Command = ReactiveCommand.Create(PlayCommand)
                    },
                    new NativeMenuItem
                    {
                        Header = "Stop",
                        Command = ReactiveCommand.Create(StopCommand)
                    },
                    new NativeMenuItem
                    {
                        Header = "Prev",
                        Command = ReactiveCommand.Create(PrevCommand)
                    },
                    new NativeMenuItem
                    {
                        Header = "Next",
                        Command = ReactiveCommand.Create(NextCommand)
                    },
                    new NativeMenuItemSeparator(),
                    new NativeMenuItem
                    {
                        Header = "Exit",
                        Command = ReactiveCommand.Create(ExitCommand)
                    }
                ]
            };

            vm.WhenAnyValue(x => x.SongInfo, x => x.PlayingIndex).Subscribe(x =>
            {
                if (x.Item2 != -1)
                {
                    trayIcon.ToolTipText = vm.SelectedStation.Name;

                    var songInfo = x.Item1;

                    if (!songInfo.IsEmpty())
                    {
                        trayIcon.ToolTipText += Environment.NewLine + songInfo.Artist + " / " + songInfo.Title;
                    }
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