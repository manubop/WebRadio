using System;

namespace WebRadio.ViewModels
{
    public partial class ConfirmationViewModel(string message, Action<bool> close) : ViewModelBase
    {
        public string Message => message;

        public void Confirm() => close(true);

        public void Cancel() => close(false);
    }
}
