using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Data;
using System.Windows.Controls;
using System.Globalization;

namespace Confuser
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
        static App()
        {
            //DARKNESS!!!
            SolidColorBrush current = SystemColors.HighlightBrush;
            FieldInfo colorCacheField = typeof(SystemColors).GetField("_colorCache",
                BindingFlags.Static | BindingFlags.NonPublic);
            Color[] _colorCache = (Color[])colorCacheField.GetValue(typeof(SystemColors));
            _colorCache[14] = Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);

            AppDomain.CurrentDomain.UnhandledException += UnhandledException;
        }

        static void UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception;
            var result = MessageBox.Show(string.Format(
@"Unhandled exception!
Message : {0}
Stack Trace : {1}", ex.Message, ex.StackTrace), "Confuser", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}