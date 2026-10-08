using System.Threading.Tasks;
using Avalonia.Controls;
using WebRadio.Views;
using WebRadio.ViewModels;

namespace WebRadio.Services
{
    public interface IDialogService
    {
        Task<bool> ShowConfirmationDialog(string message);
    }

    sealed class DialogService : IDialogService
    {
        public Window? Parent { get; set; }

        public async Task<bool> ShowConfirmationDialog(string message)
        {
            if (TopLevel.GetTopLevel(Parent) is not Window topLevel)
            {
                return false;
            }

            var dialog = new ConfirmationView();

            dialog.DataContext = new ConfirmationViewModel(message, () => dialog.Close(true), () => dialog.Close(false));

            var result = await dialog.ShowDialog<bool?>(topLevel);

            return result == true;
        }
    }
}
