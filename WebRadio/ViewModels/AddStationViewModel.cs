using System;
using CommunityToolkit.Mvvm.Input;

using WebRadio.DataModel;

namespace WebRadio.ViewModels
{
    public class AddStationViewModel : ViewModelBase
    {
        private string _name = string.Empty;
        private string _url = string.Empty;
        private string _description = string.Empty;
        private string _api = string.Empty;

        private readonly Action<StationModel> _stationCreated;

        public string Name
        {
            get => _name;
            set
            {
                if (SetProperty(ref _name, value))
                {
                    OkCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public string Url
        {
            get => _url;
            set
            {
                if (SetProperty(ref _url, value))
                {
                    OkCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        public string Api
        {
            get => _api;
            set => SetProperty(ref _api, value);
        }

        public IRelayCommand OkCommand { get; }

        public IRelayCommand CancelCommand { get; }

        public AddStationViewModel(Action<StationModel> stationCreated)
        {
            _stationCreated = stationCreated;

            OkCommand = new RelayCommand(CreateStation, CanCreateStation);
            CancelCommand = new RelayCommand(() => _stationCreated(StationModel.Empty));
        }

        private bool CanCreateStation() => StationModel.IsValid(Name, Url);

        private void CreateStation()
        {
            _stationCreated(new StationModel
            {
                Name = Name,
                Url = Url,
                Description = Description,
                Api = Api
            });
        }
    }
}
