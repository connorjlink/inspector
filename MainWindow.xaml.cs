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
            _viewmodel.ConsoleData.CollectionChanged += ConsoleData_OnCollectionChanged;
            _viewmodel.AllMessagesData.CollectionChanged += AllMessagesData_OnCollectionChanged;

            _viewmodel.ConsoleText = "help";
            _viewmodel.ExecuteCommand();
        }

        /// <summary>
        /// 
        /// </summary>
        private void ConsoleData_OnCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Check if new items were added
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                ScrollToBottom(consoleData);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void AllMessagesData_OnCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Check if new items were added
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                ScrollToBottom(allMessagesData);
            }
        }



        private void ScrollToBottom(DependencyObject view)
        {
            // Get the ScrollViewer from the ListView/DataGrid
            var scrollViewer = FindVisualChild<ScrollViewer>(view);

            if (scrollViewer != null)
            {
                scrollViewer.ScrollToBottom();
            }
        }

        private static T FindVisualChild<T>(DependencyObject depObj) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                if (child is T target)
                {
                    return target;
                }

                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                {
                    return childOfChild;
                }
            }

            return null;
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


        private void CommandText_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                _viewmodel.ExecuteCommand();
                e.Handled = true;
            }
        }
    }
}