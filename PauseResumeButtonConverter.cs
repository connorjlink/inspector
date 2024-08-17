using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows;
using MahApps.Metro.IconPacks;

namespace inspector
{
    // AI-assisted 
    public class PauseResumeButtonConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                // if the message is currently paused, show the play icon & vice versa
                return boolValue ? PackIconEntypoKind.ControllerPlay : PackIconEntypoKind.ControllerPaus;
            }

            throw new NotImplementedException();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
