using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;

namespace Confuser
{
    /// <summary>
    /// Interaction logic for Success.xaml
    /// </summary>
    public partial class Success : Page, IPage<string>
    {
        public Success()
        {
            InitializeComponent();
            Style = FindResource(typeof(Page)) as Style;
        }

        IHost host;
        string parameter;
        public void Init(IHost host, string parameter)
        {
            this.host = host;
            this.parameter = parameter;
        }

        private void CommandLink_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(parameter);
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
