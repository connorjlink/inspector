using System.Collections.ObjectModel;
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
            this.DataContext = new ViewModel();

            // TODO: fix the program crash when using these and trying to clear the lists
            //_viewmodel.ConsoleData.CollectionChanged += ConsoleData_CollectionChanged;
            //_viewmodel.AllMessagesData.CollectionChanged += AllMessagesData_CollectionChanged;
        }

        //private void ConsoleData_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        //{
        //    // autoscroll the list to the most recent items
        //    consoleData.ScrollIntoView(_viewmodel.ConsoleData.ElementAt(_viewmodel.ConsoleData.Count() - 1));
        //}

        //private void AllMessagesData_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        //{
        //    // autoscroll the data grid to the most recent items
        //    allMessagesData.ScrollIntoView(allMessagesData.Items[allMessagesData.Items.Count - 1]);
        //}


        private void ConnectButton_Clicked(object sender, RoutedEventArgs e)
        {
            _viewmodel.ConnectDisconnect();
        }


        private void SubscribeButton_Clicked(object sender, RoutedEventArgs e)
        {
            _viewmodel.SubscribeUnsubscribe();
        }


        private void PublishButton_Clicked(object sender, RoutedEventArgs e)
        {
            _viewmodel.Publish();
        }


        private void PauseButton_Clicked(object sender, RoutedEventArgs e)
        {
            _viewmodel.PauseResume();
        }


        private void PauseAllButton_Clicked(object sender, RoutedEventArgs e)
        {
            _viewmodel.PauseResumeAll();
        }


        private void KillAllButton_Clicked(object sender, RoutedEventArgs e)
        {
            _viewmodel.KillAll();
        }


        private void ExecuteCommandButton_Clicked(object sender, RoutedEventArgs e)
        {
            _viewmodel.ExecuteCommand();
        }


        private void ClearDataButton_Clicked(object sender, RoutedEventArgs e)
        {
            _viewmodel.ClearData();
        }


        private void ClearConsoleButton_Clicked(object sender, RoutedEventArgs e)
        {
            _viewmodel.ClearConsole();
        }


        private void SilenceNotification(object sender, RoutedEventArgs e)
        {
            _viewmodel.SilenceNotification();
        }


        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _viewmodel.Closing(sender, e);
        }
    }
}