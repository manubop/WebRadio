using Avalonia.Controls;
using ReactiveUI;

namespace WebRadio.ViewModels
{
    public partial class ConfirmationDialogViewModel(Window dialog, string message) : ReactiveObject
    {
        public string Message => message;

        public void Confirm() => dialog.Close(true);

        public void Cancel() => dialog.Close(false);
    }
}
