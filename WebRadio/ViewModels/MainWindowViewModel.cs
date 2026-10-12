using System;

using Microsoft.Extensions.Logging;

using WebRadio.DataModel;
using WebRadio.Services;
using WebRadio.Utils;

namespace WebRadio.ViewModels
{
    public interface IStationEditor
    {
        void AddItem();
        void EditItem(IStationModel current);
        void RemoveItem(int index, string message);
        void About();
    }

    public class MainWindowViewModel : ViewModelBase, IStationEditor
    {
        private ViewModelBase _contentViewModel;

        public MainWindowViewModel(IStationService service, IOptions options, ILoggerFactory loggerFactory, ISongDownloaderFactory songDownloaderFactory)
        {
            Stations = new(service, options, loggerFactory, songDownloaderFactory, this);

            _contentViewModel = Stations;
        }

        public StationsViewModel Stations { get; }

        public ViewModelBase ContentViewModel
        {
            get => _contentViewModel;
            private set => SetProperty(ref _contentViewModel, value);
        }

        private static bool CanCreateStation(IStationModel station) => !string.IsNullOrEmpty(station.Name) && Uri.IsWellFormedUriString(station.Url, UriKind.Absolute);

        public void AddItem()
        {
            void Handler(IStationModel station, bool ok)
            {
                if (ok)
                {
                    Stations.Model.Add(new StationModel(station));
                    Stations.SelectedIndex = Stations.Model.Count - 1;
                }

                ContentViewModel = Stations;
            }

            ContentViewModel = new EditStationViewModel(Handler, CanCreateStation);
        }

        public void EditItem(IStationModel current)
        {
            void Handler(IStationModel station, bool ok)
            {
                if (ok)
                {
                    var selectedIndex = Stations.SelectedIndex;

                    Stations.Model[selectedIndex] = new StationModel(station);

                    Stations.SelectedIndex = selectedIndex;
                }

                ContentViewModel = Stations;
            }

            ContentViewModel = new EditStationViewModel(Handler, CanCreateStation)
            {
                Name = current.Name,
                Url = current.Url,
                Description = current.Description,
                Api = current.Api,
            };
        }

        public void RemoveItem(int index, string message)
        {
            ContentViewModel = new ConfirmationViewModel(message, (confirmed) =>
            {
                if (confirmed)
                {
                    Stations.Model.RemoveAt(index);
                }

                ContentViewModel = Stations;
            });
        }

        public void About()
        {
            var aboutViewModel = new AboutViewModel(() =>
            {
                ContentViewModel = Stations;
            });

            ContentViewModel = aboutViewModel;
        }
    }
}
