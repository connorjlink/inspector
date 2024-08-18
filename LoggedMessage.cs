using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace inspector
{
    public class LoggedMessage
    {
        public LoggedMessage(float timestamp, string topic, string message, int qos)
        {
            Timestamp = timestamp;
            Topic = topic;
            Message = message;
            QoS = qos;
        }

        public float Timestamp { get; set; }
        public string Topic { get; set; }
        public string Message { get; set; }
        public int QoS { get; set; }
    }
}
