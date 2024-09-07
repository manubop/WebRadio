using Avalonia.Controls;

namespace WebRadio.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            Hide();

            ShowInTaskbar = false;

            e.Cancel = true;
        }
    }
}