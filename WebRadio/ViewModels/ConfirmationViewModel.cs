using System;

namespace WebRadio.ViewModels
{
    public partial class ConfirmationViewModel(string message, Action confirm, Action cancel) : ViewModelBase
    {
        public string Message => message;

        public void Confirm() => confirm();

        public void Cancel() => cancel();
    }
}
