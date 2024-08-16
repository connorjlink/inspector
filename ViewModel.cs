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
using System.Collections.Specialized;
using MQTTnet.Protocol;
using MQTTnet.Server;

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

        private int SubscribeQoSInt
        {
            get
            {
                // NOTE: for all the listed options, the QoS integer is the first character
                char value = SubscribeQoS[0];
                // ASCII hackery ;)
                return value - '0';
            }
        }

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
                OnPropertyChanged(nameof(SubscribeQoSInt));
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
        private static MQTTnet.Client.MqttClient _mqttClient;

        public ViewModel()
        {
            _mqttFactory = new MqttFactory();
            _mqttClient = (MQTTnet.Client.MqttClient)_mqttFactory.CreateMqttClient();

            SubscribedTopics.CollectionChanged += UpdateSubscribeInputs;
        }

        private void HandleMissing(string whatsMissing, string context, ref bool hadError)
        {
            WriteConsole($"Specify a {whatsMissing} to {context}", ERROR);
            hadError = true;
        }

        public async void Connect()
        {
            bool hadError = false;

            const string context = "connect";

            if (IP == "") HandleMissing("broker IP", context, ref hadError);
            if (Port == "") HandleMissing("broker port", context, ref hadError);

            if (EnableTLS)
            {
                if (CACert == "") HandleMissing("root CA certificate", context, ref hadError);
                if (ClientCert == "") HandleMissing("client certificate", context, ref hadError);
                if (PrivateKey == "") HandleMissing("private key", context, ref hadError);
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
            }

            catch
            {
                // TODO: display more meaningful error message here if not connected
                // (Result Code: {response.ResultCode})
                WriteConsole($"Could not disconnect from {IP}:{Port}", ERROR);
            }

            EndTask();
        }

        private void UpdateSubscribeInputs(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(IsSubscribedToCurrent));
            OnPropertyChanged(nameof(EnableQoS));
        }

        private bool ValidateSubscribeInputs(string context)
        {
            bool hadError = false;

            if (SubscribeTopic == "") HandleMissing("subscription topic", context, ref hadError);
            if (SubscribeQoS == "") HandleMissing("subscription QoS", context, ref hadError);

            // inverting because we only validate if no errors occurred
            return !hadError;
        }

        public async void Subscribe()
        {
            if (ValidateSubscribeInputs("subscribe"))
            {
                try
                {
                    BeginTask("Subscribing to MQTT topic");

                    // NOTE: MQTT.net enum definition is compatible with straight integers so cast is OK
                    // AtMostOnce = 0x00,
                    // AtLeastOnce = 0x01,
                    // ExactlyOnce = 0x02

                    // TODO: add a checkbox to set NoLocal (we don't get our own messages)

                    var mqttSubscribeOptions = new MqttClientSubscribeOptionsBuilder()
                        .WithTopicFilter(SubscribeTopic, (MqttQualityOfServiceLevel)SubscribeQoSInt, false)
                            .Build();

                    var response = await _mqttClient.SubscribeAsync(mqttSubscribeOptions);

                    //foreach (var item in response.Items)
                    //{
                    //    item.ResultCode;
                    //}

                    SubscribedTopics.Add(SubscribeTopic);
                    WriteConsole($"Subscribed to {SubscribeTopic} with QoS {SubscribeQoS}", INFO);
                }

                catch
                {
                    WriteConsole($"Could not subscribe to {SubscribeTopic}", ERROR);
                }

                EndTask();
            }
        }

        public async void Unsubscribe()
        {
            if (ValidateSubscribeInputs("unsubscribe"))
            {
                try
                {
                    BeginTask("Unsubscribing from MQTT topic");

                    var mqttUnsubscribeOptions = new MqttClientUnsubscribeOptionsBuilder()
                        .WithTopicFilter(SubscribeTopic)
                            .Build();

                    var response = await _mqttClient.UnsubscribeAsync(mqttUnsubscribeOptions);
                    //TODO: error handling if something went from unsubscribing here

                    SubscribedTopics.Remove(SubscribeTopic);
                    WriteConsole($"Unsubscribed from {SubscribeTopic}", INFO);
                }

                catch
                {
                    WriteConsole($"Could not unsubscribe from {SubscribeTopic}", ERROR);
                }

                EndTask();
            }
        }


        // used for publishing parameters
        private string _publishTopic = string.Empty;
        private string _publishQoS = string.Empty;
        private string _publishMessage = string.Empty;

        private bool _retainFlag = false;
        private bool _isPeriodic = false;
        private string _periodicRate = string.Empty;

        public string PeriodicRate
        {
            get
            {
                return _periodicRate;
            }

            set
            {
                _periodicRate = value;
                OnPropertyChanged(nameof(PeriodicRate));
            }
        }

        public bool IsPeriodic
        {
            get
            {
                return _isPeriodic;
            }

            set
            {
                _isPeriodic = value;
                OnPropertyChanged(nameof(IsPeriodic));
                OnPropertyChanged(nameof(PublishStatus));
            }
        }

        public bool RetainFlag
        {
            get
            {
                return _retainFlag;
            }

            set
            {
                _retainFlag = value;
                OnPropertyChanged(nameof(RetainFlag));
            }
        }

        public string PublishMessage
        {
            get
            {
                return _publishMessage;
            }

            set
            {
                _publishMessage = value;
                OnPropertyChanged(nameof(PublishMessage));
            }
        }

        public string PublishQoS
        {
            get
            {
                return _publishQoS;
            }

            set
            {
                _publishQoS = value;
                OnPropertyChanged(nameof(PublishQoS));
                OnPropertyChanged(nameof(PublishQoSInt));
            }
        }

        public int PublishQoSInt
        {
            get
            {
                // NOTE: for all the listed options, the QoS integer is the first character
                char value = PublishQoS[0];
                // ASCII hackery ;)
                return value - '0';
            }
        }

        public string PublishTopic
        {
            get
            {
                return _publishTopic;
            }

            set
            {
                _publishTopic = value;
                OnPropertyChanged(nameof(PublishTopic));
            }
        }

        public bool IsTransmitting
        {
            get
            {
                return false;
            }
        }

        public async void Publish()
        {
            try
            {
                BeginTask("Publishing MQTT Topic");

                switch (PublishFormat)
                {
                    case "String":
                        {
                            var applicationMessage = new MqttApplicationMessageBuilder()
                                .WithTopic(PublishTopic)
                                .WithPayload(PublishMessage)
                                .WithRetainFlag(RetainFlag)
                                .WithQualityOfServiceLevel((MqttQualityOfServiceLevel)PublishQoSInt)
                                .Build();

                            var response = await _mqttClient.PublishAsync(applicationMessage);

                            //var response = await _mqttClient.PublishStringAsync(PublishTopic, PublishMessage, (MqttQualityOfServiceLevel)PublishQoSInt, RetainFlag);
                            WriteConsole($"Published {PublishMessage} to {PublishTopic} at {PublishQoS} with retain {RetainFlag}", INFO);
                        }
                        break;

                    case "Binary":
                        {
                            //_mqttClient.PublishAsyncBinary();
                        }
                        break;

                    case "Protobuf3":
                        {

                        }
                        break;
                }
            }

            catch
            {
                WriteConsole($"Could not publish {PublishTopic}", ERROR);
            }

            EndTask();
        }

        public string PublishStatus
        {
            get
            {
                if (IsPeriodic)
                {
                    if (IsTransmitting)
                    {
                        return "Stop Transmitting";
                    }

                    return "Start Transmitting";
                }
                
                return "Publish";
            }
        }

        // string, binary, or protocol buffers 3
        private string _publishFormat = string.Empty;

        public string PublishFormat
        {
            get
            {
                return _publishFormat;
            }

            set
            {
                _publishFormat = value;
                OnPropertyChanged(nameof(PublishFormat));
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