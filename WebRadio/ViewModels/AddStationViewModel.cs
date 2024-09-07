using System.Reactive;

using ReactiveUI;

using WebRadio.DataModel;

namespace WebRadio.ViewModels
{
    public class AddStationViewModel : ViewModelBase
    {
        private string _name = string.Empty;
        private string _url = string.Empty;
        private string _description = string.Empty;
        private string _api = string.Empty;

        public string Name
        {
            get => _name;
            set => this.RaiseAndSetIfChanged(ref _name, value);
        }

        public string Url
        {
            get => _url;
            set => this.RaiseAndSetIfChanged(ref _url, value);
        }

        public string Description
        {
            get => _description;
            set => this.RaiseAndSetIfChanged(ref _description, value);
        }

        public string Api
        {
            get => _api;
            set => this.RaiseAndSetIfChanged(ref _api, value);
        }

        public ReactiveCommand<Unit, StationModel> OkCommand { get; }

        public ReactiveCommand<Unit, StationModel> CancelCommand { get; }

        public AddStationViewModel()
        {
            var isValidObservable = this.WhenAnyValue(x => x.Name, x => x.Url, (name, url) => StationModel.IsValid(name, url));

            OkCommand = ReactiveCommand.Create(
                () => new StationModel { Name = Name, Url = Url, Description = Description, Api = Api }, isValidObservable
            );
            CancelCommand = ReactiveCommand.Create(() => StationModel.Empty);
        }
    }
}
