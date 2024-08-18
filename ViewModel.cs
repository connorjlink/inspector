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
using System;
using System.Security.RightsManagement;
using System.Text;
using System.Runtime.CompilerServices;
using System.Timers;

namespace inspector
{
    public class ViewModel : INotifyPropertyChanged
    {
        private const string NO_BROKER_CONNECTION = "Not connected to a broker";

        public void WriteConsoleImpl(string message, LogLevel loglevel, ViewModel viewmodel)
        {
            if (loglevel == LogLevel.Error)
            {
                viewmodel.ShowNotification = true;
                viewmodel.NotificationCount++;
            }

            var level = loglevel switch
            {
                LogLevel.Info => "INFO",
                LogLevel.Warning => "WARNING",
                LogLevel.Error => "ERROR",
                _ => throw new NotImplementedException(),
            };
            viewmodel.ConsoleData.Add($"{Timestamp()} {level}: {message}");
        }

        private Queue<int> _sendingPerSecondQueue = new();
        private Queue<int> _receivingPerSecondQueue = new();

        public string SendingPerSecond
        {
            get
            {
                if (Connected)
                {
                    if (_sendingPerSecondQueue.Count > 0)
                    {
                        return $"{_sendingPerSecondQueue.Average()}/s";
                    }

                    return "0/s";
                }

                return "N/A";
            }
        }

        public string ReceivingPerSecond
        {
            get
            {
                if (Connected)
                {
                    if (_receivingPerSecondQueue.Count > 0)
                    {
                        return $"{_receivingPerSecondQueue.Average()}/s";
                    }

                    return "0/s";
                }

                return "N/A";
            }
        }

        private int _lastSentCount = 0;
        public int _currentSentCount = 0;

        private int _lastReceivedCount = 0;
        private int _currentReceivedCount = 0;

        // number of seconds over which we average message txfer rates
        private const int HISTORY_LENGTH = 2;

        private void ComputeSendingPerSecond()
        {
            var diff = _currentSentCount - _lastSentCount;
            _lastSentCount = _currentSentCount;

            _sendingPerSecondQueue.Enqueue(diff);

            if (_sendingPerSecondQueue.Count > HISTORY_LENGTH)
            {
                _sendingPerSecondQueue.Dequeue();
            }

            OnPropertyChanged(nameof(SendingPerSecond));
        }

        

        public string ConnectionToolTip
        {
            get
            {
                if (Connected)
                {
                    var state = EnableTLS ? "enabled" : "disabled";
                    return $"TLS encryption is {state}";
                }

                return NO_BROKER_CONNECTION;
            }
        }

        bool _areAllPaused = false;

        public string PauseAllStatus
        {
            get
            {
                return AreAllPaused ? "Resume All" : "Pause All";
            }
        }

        public bool AreAllPaused
        {
            get
            {
                return _areAllPaused;
            }

            set
            {
                _areAllPaused = value;
                OnPropertyChanged(nameof(AreAllPaused));
                OnPropertyChanged(nameof(PauseAllStatus));
            }
        }

        public void PauseAll()
        {
            _mqttScheduler.PauseAll();
            AreAllPaused = true;
            UpdatePublishTab();
        }

        public void ResumeAll()
        {
            _mqttScheduler.ResumeAll();
            AreAllPaused = false;
            UpdatePublishTab();
        }

        public void KillAll()
        {
            _mqttScheduler.KillAll();
            UpdatePublishTab();
        }

        // determines whether the user can pause/resume/kill all periodic messages
        public bool CanModifyAll
        {
            get
            {
                return _mqttScheduler.TotalMessageCount() > 0;
            }
        }

        public string UploadDownloadToolTip
        {
            get
            {
                if (Connected)
                {
                    return $"Sending {_sendingPerSecondQueue.Average()} messages/second over {_mqttScheduler.ScheduledMessageCount()} topics\n" +
                        $"Receiving {_receivingPerSecondQueue.Average()} messages/second";
                }

                return NO_BROKER_CONNECTION;
            }
        }

        private void ComputeRecievingPerSecond()
        {
            var diff = _currentReceivedCount - _lastReceivedCount;
            _lastReceivedCount = _currentReceivedCount;

            _receivingPerSecondQueue.Enqueue(diff);

            if (_receivingPerSecondQueue.Count > HISTORY_LENGTH)
            {
                _receivingPerSecondQueue.Dequeue();
            }

            OnPropertyChanged(nameof(ReceivingPerSecond));
        }

        private void ComputeMessagesPerSecond(Object source, ElapsedEventArgs e)
        {
            ComputeRecievingPerSecond();
            ComputeSendingPerSecond();
            OnPropertyChanged(nameof(UploadDownloadToolTip));
        }

        public void WriteConsole(string message, string level)
        {
            WriteConsoleImpl(message, level, this);
        }

        // TODO: support multiple tasks (i.e., nesting)
        private void BeginJob(string message)
        {
            ProgressText = message;
            ShowProgress = true;
        }

        private void EndJob()
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

        private int SubscribeQoSInt
        {
            get
            {
                return StringToQoS(SubscribeQoS);
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
                OnPropertyChanged(nameof(IsSubscribeQoSEditable));
            }
        }


        private string _subscribeQoS = string.Empty;
        /// <summary>
        /// SubscribeQoS stores the contents of the quality of service combobox in the Subscribe tab
        /// </summary>
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



        private bool _connected = false;
        /// <summary>
        /// Connected stores the current broker connection state
        /// </summary>
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
                OnPropertyChanged(nameof(NotConnected));
                OnPropertyChanged(nameof(ConnectionStatusExtended));
                OnPropertyChanged(nameof(ConnectionToolTip));
                OnPropertyChanged(nameof(SendingPerSecond));
                OnPropertyChanged(nameof(ReceivingPerSecond));
                OnPropertyChanged(nameof(UploadDownloadToolTip));
                OnPropertyChanged(nameof(CanModifyAll));
            }
        }


        /// <summary>
        /// NotConnected stores the inverse current broker connection state
        /// <br/>
        /// <b>USAGE: disable various controls to prevent editing when connected</b>
        /// </summary>
        public bool NotConnected
        {
            get
            {
                return !Connected;
            }
        }


        private string _ip = string.Empty;
        /// <summary>
        /// IP stores the contents of the broker IP combobox in the Connect tab
        /// </summary>
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



        private string _port = string.Empty;
        /// <summary>
        /// Port stores the contents of the broker port combobox in the Connect tab
        /// </summary>
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


        private bool _enableTLS = false;
        /// <summary>
        /// EnableTLS stores the contents of the TLS encryption checkbox in the Connect tab
        /// <br/>
        /// <b>NOTE: setter updates the extended connection status statusbar item tooltip </b>
        /// </summary>
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
                OnPropertyChanged(nameof(ConnectionToolTip));
            }
        }



        private string _caCert = string.Empty;
        /// <summary>
        /// CACert stores the contents of the CA certificate entry combobox in the Connect tab
        /// </summary>
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



        private string _clientCert = string.Empty;
        /// <summary>
        /// ClientCert stores the contents of the local/client certificate entry combobox in the Conncet tab
        /// </summary>
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



        private string _privateKey = string.Empty;
        /// <summary>
        /// PrivateKey stores the contents of the client private key cerficiate entry combobox in the Connect tab
        /// </summary>
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


        public bool IsSubscribedToCurrent
        {
            get
            {
                return SubscribedTopics.Contains(SubscribeTopic);
            }
        }

        public bool IsSubscribeQoSEditable
        {
            get
            {
                // can't edit the QoS if the message is already subscribed
                return !(Connected && IsSubscribedToCurrent);
            }
        }



        private ObservableCollection<string> _consoleData = new();
        /// <summary>
        /// ConsoleData is the item source for the listview in the Console window
        /// </summary>
        public ObservableCollection<string> ConsoleData
        {
            get
            {
                return _consoleData;
            }
        }



        private ObservableCollection<LoggedMessage> _allMessagesData = new();
        /// <summary>
        /// AllMessageData is the item source for the all messages datagrid in the Message window
        /// </summary>
        public ObservableCollection<LoggedMessage> AllMessagesData
        {
            get
            {
                return _allMessagesData;
            }
        }


        private ObservableCollection<CurrentMessage> _publishMessagesData = new();
        /// <summary>
        /// PublishMessageData is the item source for the active messages datagrid in the Publish tab
        /// </summary>
        public ObservableCollection<CurrentMessage> PublishMessagesData
        {
            get
            {
                return _publishMessagesData;
            }
        }


        private static MqttFactory _mqttFactory;
        public static MQTTnet.Client.MqttClient _mqttClient;

        private static MqttScheduler _mqttScheduler;

        public ViewModel()
        {
            _mqttFactory = new MqttFactory();
            _mqttClient = (MQTTnet.Client.MqttClient)_mqttFactory.CreateMqttClient();

            _mqttScheduler = new MqttScheduler(this);

            var timer = new System.Timers.Timer(1000);
            timer.Elapsed += ComputeMessagesPerSecond;
            timer.AutoReset = true;
            timer.Start();

            // 
            SubscribedTopics.CollectionChanged += (s, ea) =>
            {
                OnPropertyChanged(nameof(IsSubscribedToCurrent));
                OnPropertyChanged(nameof(IsSubscribeQoSEditable));
            };
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
                        BeginJob("Connecting to MQTT broker");

                        var response = await _mqttClient.ConnectAsync(mqttClientOptions, timeoutToken.Token);
                        Connected = true;
                        WriteConsole($"Connected to {IP}:{Port} (Result Code: {response.ResultCode})", INFO);

                        var concurrent = new SemaphoreSlim(Environment.ProcessorCount);

                        _mqttClient.ApplicationMessageReceivedAsync += async ea =>
                        {
                            // TODO: utilize shutdownToken for WaitASync, Task.Run

                            await concurrent.WaitAsync().ConfigureAwait(false);

                            async Task ProcessAsync()
                            {
                                try
                                {
                                    float timestamp = TimestampImpl();
                                    string topic = ea.ApplicationMessage.Topic;
                                    string message = Encoding.UTF8.GetString(ea.ApplicationMessage.PayloadSegment);
                                    int qos = (int)ea.ApplicationMessage.QualityOfServiceLevel;

                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        _currentReceivedCount++;
                                        // For example, update a TextBox or ListBox
                                        // myTextBox.Text = payload;
                                        //WriteConsole($"Received topic: {topic}", INFO);
                                        AllMessagesData.Add(new LoggedMessage(timestamp, topic, message, qos));
                                        OnPropertyChanged(nameof(AllMessagesData));
                                    });

                                    //await Task.Delay(1000);
                                }

                                catch
                                {

                                }

                                finally
                                {
                                    concurrent.Release();
                                }
                            }

                            _ = Task.Run(ProcessAsync);
                        };
                    }
                }

                catch
                {
                    // TODO: display more meaningful error message here if not connected
                    // (Result Code: {response.ResultCode})
                    WriteConsole($"Could not connect to {IP}:{Port}", ERROR);
                }

                EndJob();
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
                BeginJob("Disconnected from MQTT broker");

                await _mqttClient.DisconnectAsync();
                Connected = false;
                WriteConsole($"Disconnected from {IP}:{Port}", INFO);

                SubscribedTopics.Clear();
            }

            catch
            {
                WriteConsole($"Could not disconnect from {IP}:{Port}", ERROR);
            }

            EndJob();
        }

        private void UpdateSubscribeInputs(object? sender, NotifyCollectionChangedEventArgs e)
        {
            
        }

        private bool ValidateSubscribeInputs(string context)
        {
            bool hadError = false;

            if (SubscribeTopic == "") HandleMissing("subscription topic", context, ref hadError);
            if (SubscribeQoS == "") HandleMissing("subscription QoS", context, ref hadError);

            // inverting because we only validate if no errors occurred
            return !hadError;
        }


        /// <summary>
        /// Subscribe() spawns a job to subscribe to the topic selected in the SubscribeTopic combobox in the Subscribe tab
        /// </summary>
        public async void Subscribe()
        {
            if (ValidateSubscribeInputs("subscribe"))
            {
                try
                {
                    BeginJob("Subscribing to MQTT topic");

                    // NOTE: MQTT.net enum definition is compatible with straight integers so cast is OK
                    // AtMostOnce = 0x00,
                    // AtLeastOnce = 0x01,
                    // ExactlyOnce = 0x02

                    // TODO: add a checkbox to set NoLocal (we don't get our own messages)

                    var mqttSubscribeOptions = new MqttClientSubscribeOptionsBuilder()
                        .WithTopicFilter(SubscribeTopic, (MqttQualityOfServiceLevel)SubscribeQoSInt, false)
                            .Build();

                    var response = await _mqttClient.SubscribeAsync(mqttSubscribeOptions);
                    //TODO: error handling if something went from subscribing here

                    SubscribedTopics.Add(SubscribeTopic);
                    WriteConsole($"Subscribed to {SubscribeTopic} with QoS {SubscribeQoS}", INFO);
                }

                catch
                {
                    WriteConsole($"Could not subscribe to {SubscribeTopic}", ERROR);
                }

                EndJob();
            }
        }

        /// <summary>
        /// Unsubscribe() spawns a job to unsubscribe from the topic selected in the SubscribeTopic combobox in the Subscribe tab
        /// </summary>
        public async void Unsubscribe()
        {
            if (ValidateSubscribeInputs("unsubscribe"))
            {
                try
                {
                    BeginJob("Unsubscribing from MQTT topic");

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

                EndJob();
            }
        }



        private string _periodicRate = string.Empty;
        /// <summary>
        /// PeriodicRate stores the contents of the rate combobox associated with the IsPeriodic checkbox in the Publish tab
        /// </summary>
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


        private bool _isPeriodic = false;
        /// <summary>
        /// IsPeriodic stores the contents of the Periodic checkbox in the Publish tab
        /// <br/>
        /// <b>NOTE: setter forces an update of all the dynamic controls in the Publish tab</b>
        /// </summary>
        public bool IsPeriodic
        {
            get
            {
                return _isPeriodic;
            }

            set
            {
                _isPeriodic = value;
                UpdatePublishTab();
            }
        }

        

        private bool _retainFlag = false;
        /// <summary>
        /// RetainFlag stores the contents of the Retain checkbox in the Publish tab
        /// </summary>
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



        private string _publishMessage = string.Empty;
        /// <summary>
        /// PublishMessage stores the contents of the message/payload field in the Publish tab
        /// </summary>
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



        private string _publishQoS = string.Empty;
        /// <summary>
        /// PublishQoS stores the contents of the quality of service combobox in the Publish tab
        /// </summary>
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



        private int StringToQoS(string str)
        {
            // NOTE: for all the listed options, the QoS integer is the first character
            char value = str[0];
            // ASCII hackery ;)
            return value - '0';
        }

        public int PublishQoSInt
        {
            get
            {
                return StringToQoS(PublishQoS);
            }
        }

        private string _publishTopic = string.Empty;
        /// <summary>
        /// PublishTopic stores the contents of the topic combobox in the Publish tab
        /// </summary>
        public string PublishTopic
        {
            get
            {
                return _publishTopic;
            }

            set
            {
                _publishTopic = value;
                UpdatePublishTab();
            }
        }

        public bool IsTransmitting
        {
            get
            {
                if (_mqttScheduler.IsMessageScheduled(PublishTopic))
                {
                    return true;
                }

                return false;
            }
        }

        public bool IsPausable
        {
            get
            {
                return IsPeriodic && IsTransmitting && !AreAllPaused;
            }
        }

        public bool IsOnline
        {
            get
            {
                return IsPeriodic && IsTransmitting && !IsPaused && !AreAllPaused;
            }
        }

        public bool IsNotConnected
        {
            get
            {
                return !IsOnline;
            }
        }

        public bool IsPaused
        {
            get
            {
                if (_mqttScheduler.IsMessagePaused(PublishTopic))
                {
                    return true;
                }

                return false;
            }
        }

        public string TransmissionStatus
        {
            get
            {
                if (IsTransmitting)
                {
                    if (IsPaused)
                    {
                        return "PAUSED";
                    }

                    return "ONLINE";
                }

                if (IsPeriodic)
                {
                    return "PENDING";
                }

                return "SINGLE SHOT";
            }
        }

        public void Pause()
        {
            if (_mqttScheduler.TryPauseMessage(PublishTopic))
            {
                UpdatePublishTab();
                return;
            }

            WriteConsole($"Could not pause {PublishTopic}", ERROR);
        }

        public void Resume()
        {
            if (_mqttScheduler.TryResumeMessage(PublishTopic))
            {
                UpdatePublishTab();
                return;
            }

            WriteConsole($"Could not resume {PublishTopic}", ERROR);
        }

        public async void Publish()
        {
            bool hadError = false;

            const string context = "publish";

            if (PublishTopic == "") HandleMissing("topic", context, ref hadError);
            if (PublishQoS == "") HandleMissing("quality of service", context, ref hadError);
            if (IsPeriodic)
            {
                if (PeriodicRate == "") HandleMissing("periodic interval", context, ref hadError);
            }

            if (hadError)
            {
                return;
            }

            try
            {
                BeginJob("Publishing MQTT Topic");

                switch (PublishFormat)
                {
                    case "String":
                        {
                            if (!IsPeriodic)
                            {
                                var applicationMessage = new MqttApplicationMessageBuilder()
                                    .WithTopic(PublishTopic)
                                        .WithPayload(PublishMessage)
                                            .WithRetainFlag(RetainFlag)
                                                .WithQualityOfServiceLevel((MqttQualityOfServiceLevel)PublishQoSInt)
                                                    .Build();

                                //var response = await _mqttClient.PublishStringAsync(PublishTopic, PublishMessage, (MqttQualityOfServiceLevel)PublishQoSInt, RetainFlag);

                                var response = await _mqttClient.PublishAsync(applicationMessage);
                                _currentSentCount++;
                                var retain = RetainFlag ? "with" : "without";
                                WriteConsole($"Published {PublishMessage} to {PublishTopic} at QoS {PublishQoS} {retain} retain", INFO);
                            }

                            else
                            {
                                // TODO: add support for other publish formats (binary, protobuf) here!
                                if (!IsTransmitting)
                                {
                                    _mqttScheduler.ScheduleMessage(PublishTopic, PublishMessage, (MqttQualityOfServiceLevel)PublishQoSInt, RetainFlag, int.Parse(PeriodicRate));
                                }

                                else
                                {
                                    _mqttScheduler.TryRemoveMessageSchedule(PublishTopic);
                                }
                            }
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

            UpdatePublishTab();
            EndJob();
        }

        public void UpdatePublishTab()
        {
            OnPropertyChanged(nameof(IsPeriodic));
            OnPropertyChanged(nameof(IsTransmitting));
            OnPropertyChanged(nameof(PublishStatus));
            OnPropertyChanged(nameof(TransmissionStatus));
            OnPropertyChanged(nameof(IsPausable));
            OnPropertyChanged(nameof(IsPaused));
            OnPropertyChanged(nameof(IsOnline));
            OnPropertyChanged(nameof(IsNotConnected));
            OnPropertyChanged(nameof(CanModifyAll));
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

        private float TimestampImpl()
        {
            return Runtime.CurrentRuntime / 1000.0f;
        }

        private string Timestamp()
        {
            // ms -> s
            return $"[{TimestampImpl()}]";
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}