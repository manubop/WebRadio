using Avalonia.Controls;

namespace WebRadio.Views;

public partial class AddStationView : UserControl
{
    public AddStationView()
    {
        InitializeComponent();

        NameTextBox.AttachedToVisualTree += (_, _) => NameTextBox.Focus();
    }
}
