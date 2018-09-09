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
using Confuser.Core;

namespace Confuser
{
    /// <summary>
    /// Interaction logic for Summary.xaml
    /// </summary>
    public partial class Summary : Page, IPage<ConfuserDatas>
    {
        public Summary()
        {
            InitializeComponent();
            Style = FindResource(typeof(Page)) as Style;
        }
        IHost host;
        ConfuserDatas parameter;
        public void Init(IHost host, ConfuserDatas parameter)
        {
            this.host = host;
            this.parameter = parameter;

            summary.Text = parameter.Summary;
        }

        private void CommandLink_Click(object sender, RoutedEventArgs e)
        {
            List<string> asms = new List<string>();
            foreach (var i in parameter.Assemblies)
                asms.Add(i.MainModule.FullyQualifiedName);
            parameter.Parameter.SourceAssemblies = asms.ToArray();
            parameter.Parameter.StrongNameKeyPath = parameter.StrongNameKey;
            parameter.Parameter.DestinationPath = parameter.OutputPath;
            host.Go<ConfuserParameter>(new Progress(), parameter.Parameter);
        }
    }
}
