using MQTTnet.Client;
using MQTTnet;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Security.Permissions;
using System.Windows;
using System.Configuration;

namespace inspector
{
    public class ViewModel : INotifyPropertyChanged
    {
        private bool _connected = false;

        // host configuration
        private string _ip = string.Empty;
        private string _port = string.Empty;

        // tls configuration
        private bool _enableTLS = false;
        private string _caCert = string.Empty;
        private string _clientCert = string.Empty;
        private string _privateKey = string.Empty;

        // stores our status messages
        private ObservableCollection<string> _consoleOutput = new();

        public static string INFO = "INFO";
        public static string WARNING = "WARNING";
        public static string ERROR = "ERROR";

        public void WriteConsole(string message, string level)
        {
            if (level == ERROR)
            {
                ShowNotification = true;
                NotificationCount++;
            }

            ConsoleOutput.Add($"{Timestamp()} {level}: {message}");
        }

        // TODO: support multiple tasks (i.e., nesting)
        private void BeginTask(string message)
        {
            ProgressText = message;
            ShowProgress = true;
        }

        private void EndTask()
        {
            ShowProgress = false;
        }

        // notifcation summary for the console
        private bool _showNotification = false;
        private int _notificationCount = 0;

        public bool ShowNotification
        {
            get
            {
                return _showNotification;
            }

            set
            {
                _showNotification = value;
                OnPropertyChanged(nameof(ShowNotification));
            }
        }

        public int NotificationCount
        {
            get
            {
                return _notificationCount;
            }

            set
            {
                _notificationCount = value;
                OnPropertyChanged(nameof(NotificationCount));
            }
        }

        // progress bar for status bar
        private bool _showProgress = false;
        private string _progressText = string.Empty;

        public bool ShowProgress
        {
            get
            {
                return _showProgress;
            }

            set
            {
                _showProgress = value;
                OnPropertyChanged(nameof(ShowProgress));
            }
        }

        public string ProgressText
        {
            get
            {
                return _showProgress ? _progressText : "No tasks in progress";
            }

            set
            {
                _progressText = value;
                OnPropertyChanged(nameof(ProgressText));
            }
        }

        public string ConnectionStatusExtended
        {
            get
            {
                if (Connected)
                {
                    return $"Connected to {IP}:{Port}";
                }

                else
                {
                    return "Disconnected";
                }
            }
        }

        // used for topic subscribe/unsubscribe
        private ObservableCollection<string> _subscribedTopics = new();
        private string _subscribeTopic = string.Empty;
        private string _subscribeQoS = string.Empty;

        public ObservableCollection<string> SubscribedTopics
        {
            get
            {
                return _subscribedTopics;
            }
        }

        public string SubscribeTopic
        {
            get
            {
                return _subscribeTopic;
            }

            set
            {
                _subscribeTopic = value;
                OnPropertyChanged(nameof(SubscribeTopic));
                OnPropertyChanged(nameof(IsSubscribedToCurrent));
                OnPropertyChanged(nameof(EnableQoS));
            }
        }

        public string SubscribeQoS
        {
            get
            {
                return _subscribeQoS;
            }

            set
            {
                _subscribeQoS = value;
                OnPropertyChanged(nameof(SubscribeQoS));
            }
        }

        public bool IsSubscribedToCurrent
        {
            get
            {
                return SubscribedTopics.Contains(SubscribeTopic);
            }
        }

        public bool EnableQoS
        {
            get
            {
                // can't edit the QoS if the message is already subscribed
                return !(Connected && IsSubscribedToCurrent);
            }
        }


        // used to disable editing the IP/Port contols while we are connected
        public bool Editable
        {
            get
            {
                return !Connected;
            }
        }


        public bool Connected
        {
            get
            {
                return _connected;
            }

            set
            {
                _connected = value;
                OnPropertyChanged(nameof(Connected));
                OnPropertyChanged(nameof(ConnectionStatusExtended));
            }
        }

        public string IP
        {
            get
            {
                return _ip;
            }

            set
            {
                _ip = value;
                OnPropertyChanged(nameof(IP));
            }
        }

        public string Port
        {
            get
            {
                return _port;
            }

            set
            {
                _port = value;
                OnPropertyChanged(nameof(Port));
            }
        }

        public bool EnableTLS
        {
            get
            {
                return _enableTLS;
            }

            set
            {
                _enableTLS = value;
                OnPropertyChanged(nameof(EnableTLS));
            }
        }

        public string CACert
        {
            get
            {
                return _caCert;
            }

            set
            {
                _caCert = value;
                OnPropertyChanged(nameof(CACert));
            }
        }

        public string ClientCert
        {
            get
            {
                return _clientCert;
            }

            set
            {
                _clientCert = value;
                OnPropertyChanged(nameof(ClientCert));
            }
        }

        public string PrivateKey
        {
            get
            {
                return _privateKey;
            }

            set
            {
                _privateKey = value;
                OnPropertyChanged(nameof(PrivateKey));
            }
        }

        public ObservableCollection<string> ConsoleOutput
        {
            get
            {
                return _consoleOutput;
            }
        }


        private static MqttFactory _mqttFactory;
        private static MqttClient _mqttClient;

        public ViewModel()
        {
            _mqttFactory = new MqttFactory();
            _mqttClient = (MqttClient)_mqttFactory.CreateMqttClient();
        }

        private void handleMissing(string whatsMissing, string context, ref bool hadError)
        {
            WriteConsole($"Specify a {whatsMissing} to {context}", ERROR);
            hadError = true;
        }

        public async void Connect()
        {
            bool hadError = false;

            const string context = "connect";

            if (IP == "") handleMissing("broker IP", context, ref hadError);
            if (Port == "") handleMissing("broker port", context, ref hadError);

            if (EnableTLS)
            {
                if (CACert == "") handleMissing("root CA certificate", context, ref hadError);
                if (ClientCert == "") handleMissing("client certificate", context, ref hadError);
                if (PrivateKey == "") handleMissing("private key", context, ref hadError);
            }

            if (hadError)
            {
                return;
            }


            if (!EnableTLS)
            {
                var mqttClientOptions = new MqttClientOptionsBuilder()
                    .WithTcpServer(IP, int.Parse(Port))
                        .Build();

                try
                {
                    using (var timeoutToken = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                    {
                        BeginTask("Connecting to MQTT broker");

                        var response = await _mqttClient.ConnectAsync(mqttClientOptions, timeoutToken.Token);
                        Connected = true;
                        WriteConsole($"Connected to {IP}:{Port} (Result Code: {response.ResultCode})", INFO);

                        EndTask();
                    }
                }

                catch
                {
                    // TODO: display more meaningful error message here if not connected
                    // (Result Code: {response.ResultCode})
                    WriteConsole($"Could not connect to {IP}:{Port}", ERROR);
                }
            }

            else
            {
                WriteConsole("TLS connections are currently unsupported", ERROR);
            }
        }

        public async void Disconnect()
        {
            try
            {
                BeginTask("Disconnected from MQTT broker");

                await _mqttClient.DisconnectAsync();
                Connected = false;
                WriteConsole($"Disconnected from {IP}:{Port}", INFO);

                SubscribedTopics.Clear();
                UpdateSubscribeInputs();

                EndTask();
            }

            catch
            {
                // TODO: display more meaningful error message here if not connected
                // (Result Code: {response.ResultCode})
                WriteConsole($"Could not disconnect from {IP}:{Port}", ERROR);
            }
        }

        private void UpdateSubscribeInputs()
        {
            OnPropertyChanged(nameof(IsSubscribedToCurrent));
            OnPropertyChanged(nameof(EnableQoS));
        }

        private bool ValidateSubscribeInputs(string context)
        {
            bool hadError = false;

            if (SubscribeTopic == "") handleMissing("subscription topic", context, ref hadError);
            if (SubscribeQoS == "") handleMissing("subscription QoS", context, ref hadError);

            

            // inverting because we only validate if no errors occurred
            return !hadError;
        }

        public async void Subscribe()
        {
            //TODO: BeginTask()
            // TODO: mqttnet

            if (ValidateSubscribeInputs("subscribe"))
            {
                SubscribedTopics.Add(SubscribeTopic);
                UpdateSubscribeInputs();

                WriteConsole($"Subscribed to {SubscribeTopic} with QoS {SubscribeQoS}", INFO);
            }
        }

        public async void Unsubscribe()
        {
            //TODO: BeginTask()
            // TODO: mqttnet

            if (ValidateSubscribeInputs("unsubscribe"))
            {
                SubscribedTopics.Remove(SubscribeTopic);
                UpdateSubscribeInputs();

                WriteConsole($"Unsubscribed from {SubscribeTopic}", INFO);
            }
        }


        private string Timestamp()
        {
            // ms -> s
            return $"[{Runtime.CurrentRuntime / 1000.0f}]";
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}