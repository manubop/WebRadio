using Avalonia.Controls;
using Avalonia.Input;

using WebRadio.ViewModels;

namespace WebRadio.Views;

public partial class StationsView : UserControl
{
    public StationsView()
    {
        InitializeComponent();
    }

    void DoubleTappedHandler(object? o, TappedEventArgs a)
    {
        if (DataContext is StationsViewModel svm)
        {
            svm.PlayItem();
        }
    }
}
