using System.Threading.Tasks;
using Avalonia.Controls;
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

            var dialog = new ConfirmationDialog();
            var vm = new ConfirmationDialogViewModel(dialog, message);

            dialog.DataContext = vm;

            var result = await dialog.ShowDialog<bool?>(topLevel);

            return result == true;
        }
    }
}
