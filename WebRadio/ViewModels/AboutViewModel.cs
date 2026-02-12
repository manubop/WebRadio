using System;
using System.Reactive;
using System.Reflection;
using Avalonia;
using ReactiveUI;
using Un4seen.Bass;
using WebRadio.Utils;

namespace WebRadio.ViewModels
{
    internal sealed class AboutViewModel(Action<Unit> action) : ViewModelBase
    {
        public string Text { get; } =
            $"WebRadio {Assembly.GetExecutingAssembly().GetName().Version}\n\n" +
            "A simple web radio player application.\n" +
            $"Based on Avalonia {typeof(AvaloniaObject).Assembly.GetName().Version}\n" +
            $"And BASS {VersionHelper.GetVersion(Bass.BASS_GetVersion())}\n" +
            "© 2024 WebRadio Team";

        public ReactiveCommand<Unit, Unit> OkCommand { get; } = ReactiveCommand.Create(action);
    }
}
