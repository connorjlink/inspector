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
using System.Windows.Navigation;

namespace inspector
{
#pragma warning disable 8618
    public class ViewModel : INotifyPropertyChanged
    {
        /// <summary>
        /// Boilerplate code for the INotifyPropertyChanged interface
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


        // used for a few statusbar item hover tooltips when disconnected
        private const string NO_BROKER_CONNECTION = "Not connected to a broker";
        
        // number of seconds over which we average message txfer rates
        private const int HISTORY_LENGTH = 2;


        private static MqttFactory _mqttFactory;
        public static MQTTnet.Client.MqttClient _mqttClient;

        private static MqttScheduler _mqttScheduler;
        public static JobScheduler _jobScheduler;


        // for computing message throughput in the statusbar
        private Queue<int> _sendingPerSecondQueue = new();
        private Queue<int> _receivingPerSecondQueue = new();
        // yet more tracking to compute send/recieve rate
        private int _lastSentCount = 0;
        // TODO: make not public (currently the scheduler needs to increment this)
        // will have abstract away some of that functionality
        public int _currentSentCount = 0;

        private int _lastReceivedCount = 0;
        private int _currentReceivedCount = 0;


        public ViewModel()
        {
            _mqttFactory = new MqttFactory();
            _mqttClient = (MQTTnet.Client.MqttClient)_mqttFactory.CreateMqttClient();

            _mqttScheduler = new MqttScheduler(this);
            _jobScheduler = new JobScheduler();

            var timer = new System.Timers.Timer(1000);
            timer.Elapsed += (s, ea) => ComputeMessagesPerSecond();
            timer.AutoReset = true;
            timer.Start();

            // 
            SubscribedMessagesData.CollectionChanged += (s, ea) =>
            {
                OnPropertyChanged(nameof(IsCurrentTopicSubscribed));
                OnPropertyChanged(nameof(IsSubscribeQoSEditable));
            };


            ConsoleData.CollectionChanged += (s, ea) =>
            {
                OnPropertyChanged(nameof(ConsoleDataExists));
            };

            AllMessagesData.CollectionChanged += (s, ea) =>
            {
                OnPropertyChanged(nameof(AllMessagesDataExists));
            };
        }


        public void WriteConsole(string message, LogLevel loglevel)
        {
            if (loglevel == LogLevel.Error)
            {
                NotificationCount++;
            }

            var level = loglevel switch
            {
                LogLevel.Info => "INFO",
                LogLevel.Warning => "WARNING",
                LogLevel.Error => "ERROR",
                _ => throw new NotImplementedException(),
            };

            var formattedTimestamp = $"[{Timestamp()}]";
            ConsoleData.Add($"{formattedTimestamp} {level}: {message}");
        }


        private float Timestamp()
        {
            return Runtime.CurrentRuntime / 1000.0f;
        }


        



        #region Combobox Options Properties
        private ObservableCollection<string> QoS_OPTIONS = ["0 (At most once)", "1 (At least once)", "2 (Exactly once)"];
        /// <summary>
        /// QoSOptions returns the list of available combobox quality of service options in the Subscribe and Publish tabs
        /// </summary>
        public ObservableCollection<string> QoSOptions
        {
            get => QoS_OPTIONS;
        }


        private ObservableCollection<MessageFormat> MESSAGEFORMAT_OPTIONS = [MessageFormat.String, MessageFormat.Binary, MessageFormat.Protobuf3];
        /// <summary>
        /// MessageFormatOptions returns the list of available combobox message format options in the Publish tab
        /// </summary>
        public ObservableCollection<MessageFormat> MessageFormatOptions
        {
            get => MESSAGEFORMAT_OPTIONS;
        }
        #endregion



        #region Statusbar Helper Functions
        /// <summary>
        /// FormatRate() returns the formatted string representing the average message rate from the given datapoint queue for the Statusbar send/receive indicator
        /// </summary>
        private string FormatRate(Queue<int> queue)
        {
            if (IsConnected)
            {
                if (queue.Count > 0)
                {
                    return $"{queue.Average()}/s";
                }

                return "0/s";
            }

            return "N/A";
        }


        /// <summary>
        /// ComputeMessagesPerSecondImpl() computes the number of messages sent/received in the past second and posts it to a queue for averaging
        /// </summary>
        private void ComputeMessagesPerSecondImpl(ref int lastCount, ref int currentCount, Queue<int> queue)
        {
            var diff = currentCount - lastCount;
            lastCount = currentCount;

            queue.Enqueue(diff);

            if (queue.Count > HISTORY_LENGTH)
            {
                queue.Dequeue();
            }
        }


        /// <summary>
        /// ComputeMessagesPerSecond() computes the send/receive message rates and updates the Statusbar send/recieve indicator accordingly
        /// </summary>
        private void ComputeMessagesPerSecond()
        {
            ComputeMessagesPerSecondImpl(ref _lastSentCount, ref _currentSentCount, _sendingPerSecondQueue);
            ComputeMessagesPerSecondImpl(ref _lastReceivedCount, ref _currentReceivedCount, _receivingPerSecondQueue);

            OnPropertyChanged(nameof(ReceivingStatus));
            OnPropertyChanged(nameof(SendingStatus));

            OnPropertyChanged(nameof(SendingReceivingToolTip));
        }


        /// <summary>
        /// UpdateJobProgress() forces a refresh of the properties associated with the Statusbar job progress indicator
        /// </summary>
        private void UpdateJobProgress()
        {
            OnPropertyChanged(nameof(IsJobInProgress));
            OnPropertyChanged(nameof(JobProgressToolTip));
        }
        #endregion



        #region Statusbar Properties 
        /// <summary>
        /// ConnectionStatus stores a formatted string for the text in the Connection indicator in the statusbar
        /// </summary>
        public string ConnectionStatus
        {
            get
            {
                if (IsConnected)
                {
                    return $"Connected to {ConnectIP}:{ConnectPort}";
                }

                else
                {
                    return "Disconnected";
                }
            }
        }


        /// <summary>
        /// ConnectionStatusToolTip stores the formatted hover text for the Connection indicator in the statusbar
        /// </summary>
        public string ConnectionStatusToolTip
        {
            get
            {
                if (IsConnected)
                {
                    var state = ConnectEnableTLS ? "enabled" : "disabled";
                    return $"TLS encryption is {state}";
                }

                return NO_BROKER_CONNECTION;
            }
        }

        /// <summary>
        /// SendingStatus stores a formatted string representing the average sent messages per second
        /// <br/>
        /// <b>SEE ALSO: `HISTORY_LENGTH` for information about time duration for averaging</b>
        /// </summary>
        public string SendingStatus
        {
            get => FormatRate(_sendingPerSecondQueue);
        }


        /// <summary>
        /// ReceivingStatus stores a formatted string representing the average received messages per second
        /// <br/>
        /// <b>SEE ALSO: `HISTORY_LENGTH` for information about time duration for averaging</b>
        /// </summary>
        public string ReceivingStatus
        {
            get => FormatRate(_receivingPerSecondQueue);
        }


        /// <summary>
        /// SendingReceivingToolTip stores the formatted hover text for the Sent/Received indicator in the statusbar
        /// </summary>
        public string SendingReceivingToolTip
        {
            get
            {
                if (IsConnected)
                {
                    return $"Sending {_sendingPerSecondQueue.Average()} messages/second over {_mqttScheduler.UnpausedMessageCount()} topics\n" +
                           $"Receiving {_receivingPerSecondQueue.Average()} messages/second";
                }

                return NO_BROKER_CONNECTION;
            }
        }


        /// <summary>
        /// JobInProgress stores whether or not there is currently a pending job for the statusbar indicator
        /// </summary>
        public bool IsJobInProgress
        {
            get => (_jobScheduler.ActiveJobs.Count() > 0);
        }


        /// <summary>
        /// JobProgressToolTip stores the formatted hover text for the job progress indicator statusbar item 
        /// </summary>
        public string JobProgressToolTip
        {
            get
            {
                if (IsJobInProgress)
                {
                    var tooltip = new StringBuilder();

                    foreach (var job in _jobScheduler.ActiveJobs)
                    {
                        tooltip.AppendLine(job);
                    }

                    return tooltip.ToString().Trim();
                }

                return "No jobs in progress";
            }
        }
        #endregion



        #region Notification Properties
        private int _notificationCount = 0;
        /// <summary>
        /// NotificationCount stores the total number of unread error notifications for use in the hover tooltip
        /// </summary>
        public int NotificationCount
        {
            get => _notificationCount;

            set
            {
                _notificationCount = value;
                OnPropertyChanged(nameof(NotificationCount));
                OnPropertyChanged(nameof(ShowNotification));
                OnPropertyChanged(nameof(NotificationToolTip));
            }
        }


        /// <summary>
        /// ShowNotification stores whether or not an error notification should be displayed in the titlebar and console window
        /// </summary>
        public bool ShowNotification
        {
            get => (NotificationCount > 0);
        }


        /// <summary>
        /// NotificationToolTip stores the formatted hover text for the notification button when it appears to indicate an errro
        /// </summary>
        public string NotificationToolTip
        {
            get
            {
                var pluralize = NotificationCount != 1 ? "s" : "";
                return $"{NotificationCount} Notification{pluralize}";
            }
        }
        #endregion



        #region Item Source Properties
        private ObservableCollection<string> _consoleData = new();
        /// <summary>
        /// ConsoleData is the item source for the listview in the Console window
        /// </summary>
        public ObservableCollection<string> ConsoleData
        {
            get => _consoleData;
        }


        /// <summary>
        /// ConsoleDataExists stores whether or not there are any items the ConsoleData item source
        /// </summary>
        public bool ConsoleDataExists
        {
            get => (ConsoleData.Count > 0);
        }


        private ObservableCollection<LoggedMessage> _allMessagesData = new();
        /// <summary>
        /// AllMessageData is the item source for the all messages datagrid in the Message window
        /// </summary>
        public ObservableCollection<LoggedMessage> AllMessagesData
        {
            get => _allMessagesData;
        }


        /// <summary>
        /// AllMessagesDataExists stores whether or not there are any receieved messages logged in the Message window datagrid
        /// </summary>
        public bool AllMessagesDataExists
        {
            get => (AllMessagesData.Count > 0);
        }


        private ObservableCollection<LoggedMessage> _liveMessagesData = new();
        private Dictionary<string, LoggedMessage> _liveMessagesDictionary = new();
        /// <summary>
        /// LiveMessagesData is the item source for the live messages datagrid in the Message window
        /// </summary>
        public ObservableCollection<LoggedMessage> LiveMessagesData
        {
            get => _liveMessagesData;
        }


        private ObservableCollection<string> _subscribedMessagesData = new();
        /// <summary>
        /// SubscribedMessagesData is the item source for the subscribed messages listview in the Subscribe tab
        /// </summary>
        public ObservableCollection<string> SubscribedMessagesData
        {
            get => _subscribedMessagesData;
        }


        private ObservableCollection<CurrentMessage> _publishMessagesData = new();
        /// <summary>
        /// PublishMessageData is the item source for the active messages datagrid in the Publish tab
        /// </summary>
        public ObservableCollection<CurrentMessage> PublishMessagesData
        {
            get => _publishMessagesData;
        }
        #endregion



        #region Connect Tab Input Properties
        private string _connectIP = string.Empty;
        /// <summary>
        /// ConnectIP stores the contents of the broker IP combobox in the Connect tab
        /// </summary>
        public string ConnectIP
        {
            get => _connectIP;

            set
            {
                _connectIP = value;
                OnPropertyChanged(nameof(ConnectIP));
            }
        }


        private string _connectPort = string.Empty;
        /// <summary>
        /// ConnectPort stores the contents of the broker port combobox in the Connect tab
        /// </summary>
        public string ConnectPort
        {
            get => _connectPort;

            set
            {
                _connectPort = value;
                OnPropertyChanged(nameof(ConnectPort));
            }
        }


        private bool _connectEnableTLS = false;
        /// <summary>
        /// ConnectEnableTLS stores the contents of the TLS encryption checkbox in the Connect tab
        /// <br/>
        /// <b>NOTE: setter updates the extended connection status statusbar item tooltip </b>
        /// </summary>
        public bool ConnectEnableTLS
        {
            get => _connectEnableTLS;

            set
            {
                _connectEnableTLS = value;
                OnPropertyChanged(nameof(ConnectEnableTLS));
                OnPropertyChanged(nameof(ConnectionStatusToolTip));
            }
        }


        private string _connectCertCA = string.Empty;
        /// <summary>
        /// ConnectCertCA stores the contents of the CA certificate entry combobox in the Connect tab
        /// </summary>
        public string ConnectCertCA
        {
            get => _connectCertCA;

            set
            {
                _connectCertCA = value;
                OnPropertyChanged(nameof(ConnectCertCA));
            }
        }


        private string _connectCertClient = string.Empty;
        /// <summary>
        /// ConnectCertClient stores the contents of the local/client certificate entry combobox in the Conncet tab
        /// </summary>
        public string ConnectCertClient
        {
            get => _connectCertClient;

            set
            {
                _connectCertClient = value;
                OnPropertyChanged(nameof(ConnectCertClient));
            }
        }


        private string _connectCertPrivate = string.Empty;
        /// <summary>
        /// ConnectCertPrivate stores the contents of the client private key cerficiate entry combobox in the Connect tab
        /// </summary>
        public string ConnectCertPrivate
        {
            get => _connectCertPrivate;

            set
            {
                _connectCertPrivate = value;
                OnPropertyChanged(nameof(ConnectCertPrivate));
            }
        }
        #endregion



        #region Connect Tab Output Properties
        private bool _isConnected = false;
        /// <summary>
        /// IsConnected stores the current broker connection state
        /// </summary>
        public bool IsConnected
        {
            get => _isConnected;

            set
            {
                _isConnected = value;

                OnPropertyChanged(nameof(IsConnected));
                OnPropertyChanged(nameof(IsDisconnected));

                OnPropertyChanged(nameof(ConnectionStatus));
                OnPropertyChanged(nameof(ConnectionStatusToolTip));

                OnPropertyChanged(nameof(SendingStatus));
                OnPropertyChanged(nameof(ReceivingStatus));
                OnPropertyChanged(nameof(SendingReceivingToolTip));

                OnPropertyChanged(nameof(IsAnyTopicScheduled));
            }
        }


        /// <summary>
        /// IsDisconnected stores the inverse current broker connection state
        /// <br/>
        /// <b>USAGE: disable various controls to prevent editing when not connected</b>
        /// </summary>
        public bool IsDisconnected
        {
            get => !IsConnected;
        }
        #endregion



        #region Subscribe Tab Input Properties
        private string _subscribeTopic = string.Empty;
        /// <summary>
        /// SubscribeTopic stores the contents of the message topic combobox in the Subscribe tab
        /// </summary>
        public string SubscribeTopic
        {
            get => _subscribeTopic;
            
            set
            {
                _subscribeTopic = value;
                OnPropertyChanged(nameof(SubscribeTopic));
                OnPropertyChanged(nameof(IsCurrentTopicSubscribed));
                OnPropertyChanged(nameof(IsSubscribeQoSEditable));
            }
        }


        private MqttQualityOfServiceLevel _subscribeQoS = 0;
        /// <summary>
        /// SubscribeQoS stores the contents of the quality of service combobox in the Subscribe tab
        /// </summary>
        public MqttQualityOfServiceLevel SubscribeQoS
        {
            get => _subscribeQoS;

            set
            {
                _subscribeQoS = value;
                OnPropertyChanged(nameof(SubscribeQoS));
            }
        }
        #endregion 



        #region Subscribe Tab Output Properties
        /// <summary>
        /// IsCurrentTopicSubscribed stores whether or not the topic selected in the Subscribe tab is subscribed on the client
        /// </summary>
        public bool IsCurrentTopicSubscribed
        {
            get => SubscribedMessagesData.Contains(SubscribeTopic);
        }


        /// <summary>
        /// IsSubscribeQoSEditable stores whether or not the quality of service dropdown in the Subscribe tab can be edited
        /// <br/>
        /// <b>NOTE: the quality of service is not editable is the selected message topic is currently subscribed</b>
        /// </summary>
        public bool IsSubscribeQoSEditable
        {
            get => !(IsConnected && IsCurrentTopicSubscribed);
        }
        #endregion 



        #region Publish Tab Input Properties
        private string _publishTopic = string.Empty;
        /// <summary>
        /// PublishTopic stores the contents of the topic combobox in the Publish tab
        /// </summary>
        public string PublishTopic
        {
            get => _publishTopic;

            set
            {
                _publishTopic = value;
                UpdatePublishTab();
            }
        }


        private MqttQualityOfServiceLevel _publishQoS = 0;
        /// <summary>
        /// PublishQoS stores the contents of the quality of service combobox in the Publish tab
        /// </summary>
        public MqttQualityOfServiceLevel PublishQoS
        {
            get => _publishQoS;

            set
            {
                _publishQoS = value;
                OnPropertyChanged(nameof(PublishQoS));
            }
        }


        private string _publishMessage = string.Empty;
        /// <summary>
        /// PublishMessage stores the contents of the message/payload field in the Publish tab
        /// </summary>
        public string PublishMessage
        {
            get => _publishMessage;

            set
            {
                _publishMessage = value;
                OnPropertyChanged(nameof(PublishMessage));
            }
        }


        private MessageFormat _publishMessageFormat = MessageFormat.String;
        /// <summary>
        /// PublishMessageFormat stores the state of the message format combobox in the Publish tab
        /// </summary>
        public MessageFormat PublishMessageFormat
        {
            get => _publishMessageFormat;

            set
            {
                _publishMessageFormat = value;
                OnPropertyChanged(nameof(PublishMessageFormat));
            }
        }


        private bool _publishRetainFlag = false;
        /// <summary>
        /// PublishRetainFlag stores the contents of the Retain checkbox in the Publish tab
        /// </summary>
        public bool PublishRetainFlag
        {
            get => _publishRetainFlag;

            set
            {
                _publishRetainFlag = value;
                OnPropertyChanged(nameof(PublishRetainFlag));
            }
        }


        private bool _publishIsPeriodic = false;
        /// <summary>
        /// IsPeriodic stores the contents of the Periodic checkbox in the Publish tab
        /// <br/>
        /// <b>NOTE: setter forces an update of all the dynamic controls in the Publish tab</b>
        /// </summary>
        public bool PublishIsPeriodic
        {
            get => _publishIsPeriodic;

            set
            {
                _publishIsPeriodic = value;
                UpdatePublishTab();
            }
        }


        private string _publishPeriodicRate = string.Empty;
        /// <summary>
        /// PublishPeriodicRate stores the contents of the rate combobox associated with the IsPeriodic checkbox in the Publish tab
        /// </summary>
        public string PublishPeriodicRate
        {
            get => _publishPeriodicRate;

            set
            {
                _publishPeriodicRate = value;
                OnPropertyChanged(nameof(PublishPeriodicRate));
            }
        }
        #endregion



        #region Publish Tab Output Properties
        /// <summary>
        /// IsCurrentTopicOnline stores whether or not the topic currently selected in the Publish tab is actively transmitting
        /// </summary>
        public bool IsCurrentTopicOnline
        {
            get => PublishIsPeriodic && IsCurrentTopicScheduled && !IsCurrentTopicPaused && !IsPeriodicGloballyPaused;
        }

        /// <summary>
        /// TransmissionStatus stores the text used for the transmission status label in the Publish tab
        /// </summary>
        public string TransmissionStatus
        {
            get
            {
                if (!IsPeriodicGloballyPaused)
                {
                    if (IsCurrentTopicScheduled)
                    {
                        if (IsCurrentTopicPaused)
                        {
                            return "PAUSED";
                        }

                        return "ONLINE";
                    }

                    if (PublishIsPeriodic)
                    {
                        return "PENDING";
                    }

                    return "SINGLE SHOT";
                }

                return "ALL TOPICS PAUSED";
            }
        }


        /// <summary>
        /// PublishStatus stores the text used for the publish button in the Publish tab
        /// </summary>
        public string PublishStatus
        {
            get
            {
                if (PublishIsPeriodic)
                {
                    if (IsCurrentTopicScheduled)
                    {
                        return "Stop Transmitting";
                    }

                    return "Start Transmitting";
                }

                return "Publish";
            }
        }


        /// <summary>
        /// IsAnyTopicScheduled stores whether or not there are any periodic topics currently scheduled for transmission
        /// <br/>
        /// <b>NOTE: controls if the user can pause/resume/kill ALL periodic messages</b>
        /// </summary>
        public bool IsAnyTopicScheduled
        {
            get => (_mqttScheduler.TotalMessageCount() > 0);
        }


        /// <summary>
        /// IsCurrentTopicScheduled stores whether or not the topic currently selected in the Publish tab is scheduled to be transmitted
        /// </summary>
        public bool IsCurrentTopicScheduled
        {
            get => _mqttScheduler.IsMessageScheduled(PublishTopic);
        }


        /// <summary>
        /// IsCurrentTopicPaused stores whether or not the topic currently selected in the Publish tab was individually paused
        /// </summary>
        public bool IsCurrentTopicPaused
        {
            get => _mqttScheduler.IsMessagePaused(PublishTopic);
        }


        /// <summary>
        /// IsCurrentTopicPausable stores whether or not the topic currently selected in the Publish tab can be paused
        /// <br/>
        /// <b>NOTE: the pause/play button for individual topics is hidden if periodic topics are globally paused</b>
        /// </summary>
        public bool IsCurrentTopicPausable
        {
            get => PublishIsPeriodic && IsCurrentTopicScheduled && !IsPeriodicGloballyPaused;
        }


        bool _isPeriodicGloballyPaused = false;
        /// <summary>
        /// IsPeriodicGloballyPaused stores the global paused/running state for the Pause All/Resume All button in the Publish tab
        /// </summary>
        public bool IsPeriodicGloballyPaused
        {
            get => _isPeriodicGloballyPaused;

            set
            {
                _isPeriodicGloballyPaused = value;
                OnPropertyChanged(nameof(IsPeriodicGloballyPaused));
            }
        }
        #endregion



        #region Console Window Input Properties
        private string _consoleText = string.Empty;
        /// <summary>
        /// 
        /// </summary>
        public string ConsoleText
        {
            get => _consoleText;

            set
            {
                _consoleText = value;
                OnPropertyChanged(nameof(ConsoleText));
            }
        }
        #endregion



        #region Button Callback Helper Functions
        private void HandleMissing(string whatsMissing, string context, ref bool hadError)
        {
            WriteConsole($"Specify a {whatsMissing} to {context}", LogLevel.Error);
            hadError = true;
        }

        private bool ValidateSubscribeInputs(string context)
        {
            bool hadError = false;

            if (SubscribeTopic == "") HandleMissing("subscription topic", context, ref hadError);

            // inverting because we only validate if no errors occurred
            return !hadError;
        }

        private void UpdatePublishTab()
        {
            OnPropertyChanged(nameof(PublishTopic));
            OnPropertyChanged(nameof(PublishQoS));
            OnPropertyChanged(nameof(PublishMessage));
            OnPropertyChanged(nameof(PublishMessageFormat));
            OnPropertyChanged(nameof(PublishRetainFlag));
            OnPropertyChanged(nameof(PublishIsPeriodic));
            OnPropertyChanged(nameof(PublishPeriodicRate));
            OnPropertyChanged(nameof(PublishStatus));
            OnPropertyChanged(nameof(TransmissionStatus));
            OnPropertyChanged(nameof(IsCurrentTopicScheduled));
            OnPropertyChanged(nameof(IsCurrentTopicOnline));
            OnPropertyChanged(nameof(IsCurrentTopicPaused));
            OnPropertyChanged(nameof(IsCurrentTopicPausable));
            OnPropertyChanged(nameof(IsAnyTopicScheduled));
        }
        #endregion



        #region Button Callback Functions
        /// <summary>
        /// Connect() attempts to create an MQTT connection to the broker on selected IP and port in the Connect tab
        /// </summary>
        public async void Connect()
        {
            bool hadError = false;

            const string context = "connect";

            if (ConnectIP == "") HandleMissing("broker IP", context, ref hadError);
            if (ConnectPort == "") HandleMissing("broker port", context, ref hadError);

            if (ConnectEnableTLS)
            {
                if (ConnectCertCA == "") HandleMissing("root CA certificate", context, ref hadError);
                if (ConnectCertClient == "") HandleMissing("client certificate", context, ref hadError);
                if (ConnectCertPrivate == "") HandleMissing("private key", context, ref hadError);
            }

            if (hadError)
            {
                return;
            }


            if (!ConnectEnableTLS)
            {
                var mqttClientOptions = new MqttClientOptionsBuilder()
                    .WithTcpServer(ConnectIP, int.Parse(ConnectPort))
                    .Build();

                int jobID = jobID = _jobScheduler.BeginJob("Connecting to MQTT broker");

                try
                {
                    using (var timeoutToken = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                    {
                        var response = await _mqttClient.ConnectAsync(mqttClientOptions, timeoutToken.Token);
                        IsConnected = true;
                        WriteConsole($"Connected to {ConnectIP}:{ConnectPort} (Result Code: {response.ResultCode})", LogLevel.Info);

                        var concurrent = new SemaphoreSlim(Environment.ProcessorCount);

                        _mqttClient.ApplicationMessageReceivedAsync += async ea =>
                        {
                            // TODO: utilize shutdownToken for WaitASync, Task.Run 
                            // this should the crashing bug that happens if we are transmitting messages and hit the X button to close down 

                            await concurrent.WaitAsync().ConfigureAwait(false);

                            async Task ProcessAsync()
                            {
                                try
                                {
                                    float timestamp = Timestamp();
                                    string topic = ea.ApplicationMessage.Topic;
                                    string message = Encoding.UTF8.GetString(ea.ApplicationMessage.PayloadSegment);
                                    int qos = (int)ea.ApplicationMessage.QualityOfServiceLevel;

                                    var allMessage = new LoggedMessage(timestamp, topic, message, qos);
                                    var liveMessage = new LoggedMessage(timestamp, topic, message, qos);

                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        _currentReceivedCount++;

                                        AllMessagesData.Add(allMessage);

                                        if (_liveMessagesDictionary.TryGetValue(topic, out var storedMessage))
                                        {
                                            storedMessage.Timestamp = liveMessage.Timestamp;
                                            storedMessage.Message = liveMessage.Message;
                                            storedMessage.QoS = liveMessage.QoS;
                                        }

                                        else
                                        {
                                            _liveMessagesDictionary[topic] = liveMessage;
                                            LiveMessagesData.Add(liveMessage);
                                        }

                                        OnPropertyChanged(nameof(AllMessagesData));
                                        OnPropertyChanged(nameof(LiveMessagesData));

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
                    WriteConsole($"Could not connect to {ConnectIP}:{ConnectPort}", LogLevel.Error);
                }

                _jobScheduler.EndJob(jobID);
                UpdateJobProgress();
            }

            else
            {
                WriteConsole("TLS connections are currently unsupported", LogLevel.Error);
            }
        }


        /// <summary>
        /// Disconnect() attempts to disconnect from the broker to which the client is currently connected
        /// </summary>
        public async void Disconnect()
        {
            int jobID = jobID = _jobScheduler.BeginJob("Disconnecting from MQTT broker");

            try
            {
                await _mqttClient.DisconnectAsync();
                IsConnected = false;
                WriteConsole($"Disconnected from {ConnectIP}:{ConnectPort}", LogLevel.Info);

                SubscribedMessagesData.Clear();
                PublishMessagesData.Clear();
            }

            catch
            {
                WriteConsole($"Could not disconnect from {ConnectIP}:{ConnectPort}", LogLevel.Error);
            }

            _jobScheduler.EndJob(jobID);
            UpdateJobProgress();
        }


        /// <summary>
        /// ConnectDisconnect() connects to or disconnects from the broker on the selected IP and port in the Connect tab
        /// </summary>
        public void ConnectDisconnect()
        {
            if (IsConnected)
            {
                Disconnect();
            }

            else
            {
                Connect();
            }
        }


        /// <summary>
        /// Subscribe() spawns a job to subscribe to the topic selected in the SubscribeTopic combobox in the Subscribe tab
        /// </summary>
        public async void Subscribe()
        {
            if (ValidateSubscribeInputs("subscribe"))
            {
                int jobID = _jobScheduler.BeginJob("Subscribing to MQTT topic");

                try
                {
                    // NOTE: MQTT.net enum definition is compatible with straight integers so cast is OK
                    // AtMostOnce = 0x00,
                    // AtLeastOnce = 0x01,
                    // ExactlyOnce = 0x02

                    // TODO: add a checkbox to set NoLocal (we don't get our own messages)

                    var mqttSubscribeOptions = new MqttClientSubscribeOptionsBuilder()
                        .WithTopicFilter(SubscribeTopic, SubscribeQoS, false)
                            .Build();

                    var response = await _mqttClient.SubscribeAsync(mqttSubscribeOptions);
                    //TODO: error handling if something went from subscribing here

                    SubscribedMessagesData.Add(SubscribeTopic);
                    WriteConsole($"Subscribed to {SubscribeTopic} with QoS {SubscribeQoS}", LogLevel.Info);
                }

                catch
                {
                    WriteConsole($"Could not subscribe to {SubscribeTopic}", LogLevel.Error);
                }

                _jobScheduler.EndJob(jobID);
                UpdateJobProgress();
            }
        }


        /// <summary>
        /// Unsubscribe() spawns a job to unsubscribe from the topic selected in the SubscribeTopic combobox in the Subscribe tab
        /// </summary>
        public async void Unsubscribe()
        {
            if (ValidateSubscribeInputs("unsubscribe"))
            {
                int jobID = _jobScheduler.BeginJob("Unsubscribing from MQTT topic");

                try
                {
                    var mqttUnsubscribeOptions = new MqttClientUnsubscribeOptionsBuilder()
                        .WithTopicFilter(SubscribeTopic)
                            .Build();

                    var response = await _mqttClient.UnsubscribeAsync(mqttUnsubscribeOptions);
                    //TODO: error handling if something went from unsubscribing here

                    SubscribedMessagesData.Remove(SubscribeTopic);
                    WriteConsole($"Unsubscribed from {SubscribeTopic}", LogLevel.Info);
                }

                catch
                {
                    WriteConsole($"Could not unsubscribe from {SubscribeTopic}", LogLevel.Error);
                }

                _jobScheduler.EndJob(jobID);
                UpdateJobProgress();
            }
        }


        /// <summary>
        /// SubscribeUnsubscribe() subscribes or unsubscribes from the topic currently selected in the Subscribe tab
        /// </summary>
        public void SubscribeUnsubscribe()
        {
            if (IsCurrentTopicSubscribed)
            {
                Unsubscribe();
            }

            else
            {
                Subscribe();
            }
        }


        /// <summary>
        /// Publish() publishes the topic currently selected in the Publish tab with the specified quality of service, payload/message, and format
        /// </summary>
        public async void Publish()
        {
            bool hadError = false;

            const string context = "publish";

            if (PublishTopic == "") HandleMissing("topic", context, ref hadError);
            if (PublishIsPeriodic)
            {
                if (PublishPeriodicRate == "") HandleMissing("periodic interval", context, ref hadError);
            }

            if (hadError)
            {
                return;
            }

            int jobID = _jobScheduler.BeginJob("Publishing MQTT Topic");

            try
            {
                //var response = await _mqttClient.PublishStringAsync(PublishTopic, PublishMessage, PublishQoS, RetainFlag);
                //var response = await _mqttClient.PublishAsyncBinary();

                switch (PublishMessageFormat)
                {
                    case MessageFormat.String:
                    {
                        var applicationMessage = new MqttApplicationMessageBuilder()
                                .WithTopic(PublishTopic)
                                .WithPayload(PublishMessage)
                                .WithRetainFlag(PublishRetainFlag)
                                .WithQualityOfServiceLevel(PublishQoS)
                                .Build();

                        if (!PublishIsPeriodic)
                        {
                            var response = await _mqttClient.PublishAsync(applicationMessage);
                            _currentSentCount++;
                            var retain = PublishRetainFlag ? "with" : "without";
                            WriteConsole($"Published {PublishMessage} to {PublishTopic} at QoS {PublishQoS} {retain} retain", LogLevel.Info);
                        }

                        else
                        {
                            if (!IsCurrentTopicScheduled)
                            {
                                var periodicRate = int.Parse(PublishPeriodicRate);
                                _mqttScheduler.ScheduleMessage(applicationMessage, periodicRate);
                            }

                            else
                            {
                                _mqttScheduler.TryRemoveMessageSchedule(applicationMessage);
                            }
                        }
                    }
                    break;

                    case MessageFormat.Binary:
                    {
                        WriteConsole($"Publishing in binary format is not currently supported", LogLevel.Error);
                    }
                    break;

                    case MessageFormat.Protobuf3:
                    {
                        WriteConsole($"Publishing in Protobuf3 format is not currently supported", LogLevel.Error);
                    }
                    break;
                }
            }

            catch
            {
                WriteConsole($"Could not publish {PublishTopic}", LogLevel.Error);
            }

            UpdatePublishTab();
            _jobScheduler.EndJob(jobID);
            UpdateJobProgress();
        }


        /// <summary>
        /// Pause() individually pauses the topic currently selected in the Publish tab
        /// </summary>
        private void Pause()
        {
            if (_mqttScheduler.TryPauseMessage(PublishTopic))
            {
                UpdatePublishTab();
                return;
            }

            WriteConsole($"Could not pause {PublishTopic}", LogLevel.Error);
        }


        /// <summary>
        /// Resume() individually resumes the topic currently selected in the Publish tab
        /// </summary>
        private void Resume()
        {
            if (_mqttScheduler.TryResumeMessage(PublishTopic))
            {
                UpdatePublishTab();
                return;
            }

            WriteConsole($"Could not resume {PublishTopic}", LogLevel.Error);
        }


        /// <summary>
        /// PauseResume() individually pauses or resumes the topic currently selected in the Publish tab
        /// </summary>
        public void PauseResume()
        {
            if (IsCurrentTopicPaused)
            {
                Resume();
            }

            else
            {
                Pause();
            }
        }


        /// <summary>
        /// PauseAll() globally pauses all messages scheduled for transmission
        /// </summary>
        private void PauseAll()
        {
            _mqttScheduler.PauseAll();
            IsPeriodicGloballyPaused = true;
            UpdatePublishTab();
        }


        /// <summary>
        /// ResumeAll() globally resumes all messages scheduled for transmission
        /// </summary>
        private void ResumeAll()
        {
            _mqttScheduler.ResumeAll();
            IsPeriodicGloballyPaused = false;
            UpdatePublishTab();
        }


        /// <summary>
        /// PauseResumeAll() globally pauses or resumes all messages scheduled for transmission
        /// </summary>
        public void PauseResumeAll()
        {
            if (IsPeriodicGloballyPaused)
            {
                ResumeAll();
            }

            else
            {
                PauseAll();
            }
        }


        /// <summary>
        /// KillAll() entirely deletes all messages scheduled for transmission
        /// </summary>
        public void KillAll()
        {
            _mqttScheduler.KillAll();
            UpdatePublishTab();
        }


        /// <summary>
        /// ExecuteCommand() parses and executes the command currently selected in the console
        /// </summary>
        public void ExecuteCommand()
        {
            bool ValidateConnection(string context)
            {
                if (!IsConnected)
                {
                    WriteConsole($"Connect to a broker before {context}", LogLevel.Error);
                    return false;
                }

                return true;
            }

            int jobID = _jobScheduler.BeginJob("Executing console command");

            try
            {
                // TODO: better delimiting here since this just splits by space (and our payload might have spaces...)
                var items = ConsoleText.Split();

                var command = items[0].ToLower();

                switch (command)
                {
                case "set":
                    {
                        var property = items[1];

                        switch (property)
                        {
                        case "cacert":
                            {
                                var cert = items[2];
                                ConnectCertCA = cert;
                                WriteConsole($"Set the root CA certificate file path", LogLevel.Info);
                            } break;

                        case "clientcert":
                            {
                                var cert = items[2];
                                ConnectCertClient = cert;
                                WriteConsole($"Set the local client certificate file path", LogLevel.Info);
                            }
                            break;

                        case "privatekey":
                            {
                                var cert = items[2];
                                ConnectCertPrivate = cert;
                                WriteConsole($"Set the private key certificate file path", LogLevel.Info);
                            }
                            break;

                        default:
                            {
                                WriteConsole($"Unrecognized property `{property}`", LogLevel.Error);
                            } break;
                        }
                    }
                    break;

                case "enabletls":
                    {
                        ConnectEnableTLS = true;
                    }
                    break;

                case "disabletls":
                    {
                        ConnectEnableTLS = false;
                    }
                    break;

                case "connect":
                    {
                        var host = items[1].Split(':');
                        var ip = host[0];
                        var port = host[1];
                        ConnectIP = ip;
                        ConnectPort = port;
                        Connect();
                    }
                    break;

                case "disconnect":
                    {
                        Disconnect();
                    }
                    break;

                case "subscribe":
                    {
                        if (!ValidateConnection("subscribing")) break;

                        var message = items[1].Split(':');
                        var topic = message[0];
                        var qos = (MqttQualityOfServiceLevel)int.Parse(message[1]);
                        SubscribeTopic = topic;
                        SubscribeQoS = qos;
                        Subscribe();
                    } 
                    break;

                case "unsubscribe":
                    {
                        if (!ValidateConnection("unsubscribing")) break;

                        var message = items[1];
                        var topic = message;
                        SubscribeTopic = topic;
                        Unsubscribe();
                    }
                    break;

                case "start":
                    {
                        if (!ValidateConnection("publishing")) break;

                        var message = items[1].Split(':');
                        var topic = message[0];
                        var qos = (MqttQualityOfServiceLevel)int.Parse(message[1]);
                        PublishTopic = topic;
                        PublishQoS = qos;

                        if (IsCurrentTopicScheduled)
                        {
                            WriteConsole($"Topic must not be scheduled to start transmitting", LogLevel.Error);
                            break;
                        }

                        // TODO: trimming quotes could result in unexpected behavior in some cases...
                        var payload = items[2].Trim('"');
                        var format = (MessageFormat)int.Parse(items[3]);
                        PublishMessage = payload;
                        PublishMessageFormat = format;

                        PublishIsPeriodic = true;

                        var interval = items[4].Trim("@ms");
                        PublishPeriodicRate = interval;

                        Publish();
                    }
                    break;

                case "stop":
                    {
                        if (!ValidateConnection("publishing")) break;

                        var topic = items[1];
                        PublishTopic = topic;

                        if (!IsCurrentTopicScheduled)
                        {
                            WriteConsole($"Topic must be scheduled to stop transmitting", LogLevel.Error);
                            break;
                        }

                        Publish();
                    }
                    break;

                case "publish":
                    {
                        if (!ValidateConnection("publishing")) break;

                        var message = items[1].Split(':');
                        var topic = message[0];
                        var qos = (MqttQualityOfServiceLevel)int.Parse(message[1]);
                        PublishTopic = topic;
                        PublishQoS = qos;

                        if (IsCurrentTopicScheduled)
                        {
                            WriteConsole($"Topic must not be scheduled to transmit single shot", LogLevel.Error);
                            break;
                        }

                        var payload = items[2];
                        payload = payload.Substring(1, payload.Length - 2);
                        var format = (MessageFormat)int.Parse(items[3]);
                        PublishMessage = payload;
                        PublishMessageFormat = format;

                        PublishIsPeriodic = false;

                        Publish();
                    }
                    break;

                case "pause":
                    {
                        if (!ValidateConnection("pausing")) break;

                        var topic = items[1];
                        PublishTopic = topic;

                        if (!IsCurrentTopicScheduled)
                        {
                            WriteConsole($"Topic must be scheduled to pause individually", LogLevel.Error);
                            break;
                        }

                        Pause();
                    }
                    break;

                case "resume":
                    {
                        if (!ValidateConnection("resuming")) break;

                        var topic = items[1];
                        PublishTopic = topic;

                        if (!IsCurrentTopicScheduled)
                        {
                            WriteConsole($"Topic must be scheduled to resume individually", LogLevel.Error);
                            break;
                        }

                        Resume();
                    }
                    break;

                case "pauseall":
                    {
                        if (!ValidateConnection("pausing")) break;
                        PauseAll();
                        WriteConsole("Paused periodic message transmission globally", LogLevel.Info);
                    }
                    break;

                case "resumeall":
                    {
                        if (!ValidateConnection("resuming")) break;
                        ResumeAll();
                        WriteConsole("Resumed periodic message transmission globally", LogLevel.Info);
                    }
                    break;

                case "killall":
                    {
                        if (!ValidateConnection("killing")) break;
                        KillAll();
                        WriteConsole("Killed all periodic message transmission globally", LogLevel.Info);
                    }
                    break;

                case "silence":
                    {
                        SilenceNotification();
                        WriteConsole("Silenced all notifications", LogLevel.Info);
                    }
                    break;

                case "man":
                case "help":
                    {
                        WriteConsole("See github.com/connorjlink/inspector/README.md for help with commands", LogLevel.Info);
                    }
                    break;

                default:
                    {
                        WriteConsole($"Unrecognized command `{command}`", LogLevel.Info);
                    } 
                    break;
                }
            }

            catch
            {
                WriteConsole($"Could not execute command `{ConsoleText}`", LogLevel.Error);
            }

            ConsoleText = string.Empty;
            _jobScheduler.EndJob(jobID);
        }


        /// <summary>
        /// ClearData() clears all the data contained in the All Messages and Live Messages tabs in the Message window
        /// </summary>
        public void ClearData()
        {
            AllMessagesData.Clear();
            _liveMessagesDictionary.Clear();
            LiveMessagesData.Clear();

            OnPropertyChanged(nameof(AllMessagesData));
            OnPropertyChanged(nameof(LiveMessagesData));
        }


        /// <summary>
        /// ClearConsole() clears all the logged messages in the Console window
        /// </summary>
        public void ClearConsole()
        {
            ConsoleData.Clear();
            OnPropertyChanged(nameof(ConsoleData));
            SilenceNotification();
        }


        /// <summary>
        /// SilenceNotification() rescinds any error messages from the notification button and Console window
        /// </summary>
        public void SilenceNotification()
        {
            NotificationCount = 0;
        }


        public void Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_mqttScheduler.UnpausedMessageCount() != 0)
            {
                WriteConsole($"Kill all periodic messages to shut down", LogLevel.Error);
                e.Cancel = true;
            }
        }
        #endregion       
    }
#pragma warning restore 8618
}