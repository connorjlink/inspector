using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows;
using MQTTnet.Protocol;

namespace inspector
{
    public class QualityOfServiceConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is MqttQualityOfServiceLevel qosValue)
            {
                switch (qosValue)
                {
                    case MqttQualityOfServiceLevel.AtMostOnce: return "0 (At most once)";
                    case MqttQualityOfServiceLevel.AtLeastOnce: return "1 (At least once)";
                    case MqttQualityOfServiceLevel.ExactlyOnce: return "2 (Exactly once)";
                }
            }

            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string stringValue)
            {
                // NOTE: for all the valid options, the QoS integer is the first character
                char number = stringValue[0];
                // ASCII hackery ;)
                return (MqttQualityOfServiceLevel)(number - '0');
            }

            //else if (value is MqttQualityOfServiceLevel qosValue)
            //{
            //    switch (qosValue)
            //    {
            //        case MqttQualityOfServiceLevel.AtMostOnce: return "0 (At most once)";
            //        case MqttQualityOfServiceLevel.AtLeastOnce: return "1 (At least once)";
            //        case MqttQualityOfServiceLevel.ExactlyOnce: return "2 (Exactly once)";
            //    }
            //}

            ////return string.Empty;

            //return Binding.DoNothing;

            //if (value is string stringValue)
            //{
            //    if (stringValue == "AtMostOnce") return MqttQualityOfServiceLevel.AtMostOnce;
            //    if (stringValue == "AtLeastOnce") return MqttQualityOfServiceLevel.AtLeastOnce;
            //    if (stringValue == "ExactlyOnce") return MqttQualityOfServiceLevel.ExactlyOnce;
            //}

            return Binding.DoNothing;
        }
    }
}
