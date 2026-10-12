using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using WebRadio.DataModel;

namespace WebRadio.ViewModels
{
    public partial class EditStationViewModel : ViewModelBase, IStationModel
    {
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(OkCommand))]
        private string _name = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(OkCommand))]
        private string _url = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private string _api = string.Empty;

        public IRelayCommand OkCommand { get; }

        public IRelayCommand CancelCommand { get; }

        public EditStationViewModel(Action<IStationModel, bool> ok, Func<IStationModel, bool> isValid)
        {
            OkCommand = new RelayCommand(() => ok(this, true), () => isValid(this));
            CancelCommand = new RelayCommand(() => ok(this, false));
        }
    }
}
