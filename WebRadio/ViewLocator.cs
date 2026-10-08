using Avalonia.Controls;
using Avalonia.Controls.Templates;

using WebRadio.ViewModels;

namespace WebRadio
{
    public class ViewLocator : IDataTemplate
    {
        public Control? Build(object? param) => param switch
        {
            AboutViewModel => new Views.AboutView(),
            AddStationViewModel => new Views.AddStationView(),
            ConfirmationViewModel => new Views.ConfirmationView(),
            MainWindowViewModel => new Views.MainWindow(),
            StationsViewModel => new Views.StationsView(),
            _ => new TextBlock { Text = "Not Found: " + param?.GetType().Name }
        };

        public bool Match(object? data) => data is ViewModelBase;
    }
}
