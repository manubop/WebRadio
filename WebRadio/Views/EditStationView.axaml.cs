using Avalonia.Controls;

namespace WebRadio.Views;

public partial class EditStationView : UserControl
{
    public EditStationView()
    {
        InitializeComponent();

        NameTextBox.AttachedToVisualTree += (_, _) => NameTextBox.Focus();
    }
}
