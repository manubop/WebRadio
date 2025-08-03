using Avalonia.Controls;

namespace WebRadio.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        public void ToggleVisibility()
        {
            if (IsVisible)
            {
                Hide();
                ShowInTaskbar = false;
            }
            else
            {
                Show();
                ShowInTaskbar = true;
            }
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            Hide();

            ShowInTaskbar = false;

            e.Cancel = true;
        }
    }
}
