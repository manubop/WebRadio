using System;
using System.Reactive.Linq;

using Microsoft.Extensions.Logging;

using ReactiveUI;

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

        public MainWindowViewModel(IStationService service, Options options, ILoggerFactory loggerFactory, ISongDownloaderFactory songDownloaderFactory)
        {
            Stations = new(service, options, loggerFactory, songDownloaderFactory, this);

            _contentViewModel = Stations;
        }

        public StationsViewModel Stations { get; }

        public ViewModelBase ContentViewModel
        {
            get => _contentViewModel;
            private set => this.RaiseAndSetIfChanged(ref _contentViewModel, value);
        }

        private void AddOrEditItem(Action<StationModel> action, AddStationViewModel? addItemViewModel = null)
        {
            addItemViewModel ??= new AddStationViewModel();

            Observable.Merge(addItemViewModel.OkCommand, addItemViewModel.CancelCommand)
                .Take(1)
                .Subscribe(station =>
                {
                    if (station.IsValid())
                    {
                        action(station);
                    }
                    ContentViewModel = Stations;
                });

            ContentViewModel = addItemViewModel;
        }

        public void AddItem()
        {
            AddOrEditItem(station =>
            {
                Stations.Model.Add(station);

                Stations.SelectedIndex = Stations.Model.Count - 1;
            });
        }

        public void EditItem(StationModel current)
        {
            AddOrEditItem(
                station =>
                {
                    var selectedIndex = Stations.SelectedIndex;

                    Stations.Model[selectedIndex] = station;

                    Stations.SelectedIndex = selectedIndex;
                },
                new AddStationViewModel
                {
                    Name = current.Name,
                    Url = current.Url,
                    Description = current.Description,
                    Api = current.Api,
                }
            );
        }

        public void About()
        {
            var aboutViewModel = new AboutViewModel(_ =>
            {
                ContentViewModel = Stations;
            });

            ContentViewModel = aboutViewModel;
        }
    }
}
