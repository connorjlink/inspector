using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace inspector
{
    public class LoggedMessage : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


        private float _timestamp;
        /// <summary>
        /// Timestamp stores the time in seconds of the message receipt since application startup
        /// </summary>
        public float Timestamp 
        {
            get => _timestamp;

            set
            {
                _timestamp = value;
                OnPropertyChanged(nameof(Timestamp));
            }
        }


        private string _topic;
        /// <summary>
        /// Topic stores the topic of the message receieved
        /// </summary>
        public string Topic 
        { 
            get => _topic; 
            
            set
            {
                _topic = value;
                OnPropertyChanged(nameof(Topic));
            }
        }


        private string _message;
        /// <summary>
        /// Message stores the payload/message field of the message received
        /// </summary>
        public string Message
        {
            get => _message;

            set
            {
                _message = value;
                OnPropertyChanged(nameof(Message));
            }
        }


        private int _qos;
        /// <summary>
        /// QoS stores the quality of service of the message receieved
        /// </summary>
        public int QoS 
        { 
            get => _qos; 
            
            set
            {
                _qos = value;
                OnPropertyChanged(nameof(QoS));
            }
        }

        public LoggedMessage(float timestamp, string topic, string message, int qos)
        {
            Timestamp = timestamp;
            Topic = topic;
            Message = message;
            QoS = qos;
        }
    }
}
