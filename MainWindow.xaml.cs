using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using MahApps.Metro.Controls;
using MQTTnet;
using MQTTnet.Client;

namespace inspector
{
    public partial class MainWindow : MetroWindow
    {
        private ViewModel _viewmodel => DataContext as ViewModel;

        public MainWindow()
        {
            InitializeComponent();
            Dark.Net.DarkNet.Instance.SetWindowThemeWpf(this, Dark.Net.Theme.Auto);

            this.DataContext = new ViewModel();
            _viewmodel.ConsoleOutput.CollectionChanged += ConsoleOutput_CollectionChanged;
        }

        private void ConsoleOutput_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // autoscroll the list to the most recent items
            consoleOutput.ScrollIntoView(consoleOutput.Items[consoleOutput.Items.Count - 1]);
        }

        private void ConnectButton_Clicked(object sender, RoutedEventArgs e)
        {
            if (_viewmodel.Connected)
            {
                _viewmodel.Disconnect();
            }

            else
            {
                _viewmodel.Connect();
            }
        }

        private void SilenceNotification(object sender, RoutedEventArgs e)
        {
            _viewmodel.ShowNotification = false;
            _viewmodel.NotificationCount = 0;
        }
    }
}