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
        void EditItem(StationModel current);
        void About();
    }

    public class MainWindowViewModel : ViewModelBase, IStationEditor
    {
        private ViewModelBase _contentViewModel;

        public MainWindowViewModel(IStationService service, Options options, ILoggerFactory loggerFactory, ISongDownloaderFactory songDownloaderFactory, IDialogService dialogService)
        {
            Stations = new(service, options, loggerFactory, songDownloaderFactory, this, dialogService);

            _contentViewModel = Stations;
        }

        public StationsViewModel Stations { get; }

        public ViewModelBase ContentViewModel
        {
            get => _contentViewModel;
            private set => SetProperty(ref _contentViewModel, value);
        }

        public void AddItem()
        {
            ContentViewModel = new AddStationViewModel(Handle(station =>
            {
                Stations.Model.Add(station);

                Stations.SelectedIndex = Stations.Model.Count - 1;
            }));
        }

        public void EditItem(StationModel current)
        {
            ContentViewModel = new AddStationViewModel(Handle(station =>
            {
                var selectedIndex = Stations.SelectedIndex;

                Stations.Model[selectedIndex] = station;

                Stations.SelectedIndex = selectedIndex;
            }))
            {
                Name = current.Name,
                Url = current.Url,
                Description = current.Description,
                Api = current.Api,
            };
        }

        public void About()
        {
            var aboutViewModel = new AboutViewModel(() =>
            {
                ContentViewModel = Stations;
            });

            ContentViewModel = aboutViewModel;
        }

        private Action<StationModel> Handle(Action<StationModel> action) => station =>
        {
            if (station.IsValid())
            {
                action(station);
            }

            ContentViewModel = Stations;
        };
    }
}
