﻿using System.Configuration;
using System.Data;
using System.Windows;

namespace inspector
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Dark.Net.DarkNet.Instance.SetCurrentProcessTheme(Dark.Net.Theme.Auto);
        }
    }
}
