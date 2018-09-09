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
using Mono.Cecil;
using System.IO;
using System.Windows.Forms;
using System.Reflection;
using Confuser.Core;
using System.Windows.Threading;

namespace Confuser
{
    /// <summary>
    /// Interaction logic for Settings.xaml
    /// </summary>
    public partial class Settings : ConfuserTab, IPage
    {
        static Settings()
        {
            TitlePropertyKey.OverrideMetadata(typeof(Settings), new UIPropertyMetadata("Options"));
        }
        public Settings()
        {
            InitializeComponent();
            Style = FindResource(typeof(ConfuserTab)) as Style;
        }


        private void OutputSel_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog fd = new FolderBrowserDialog();
            if (fd.ShowDialog() != DialogResult.Cancel)
            {
                output.Text = fd.SelectedPath;
                host.Project.OutputPath = fd.SelectedPath;
            }
        }
        private void SnSel_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Strong name key file (*.snk; *.pfx)|*.snk;*.pfx|All Files (*.*)|*.*";
            if (ofd.ShowDialog() != DialogResult.Cancel)
            {
                sn.Text = ofd.FileName;
                host.Project.StrongNameKey = ofd.FileName;
            }
        }
        private void LoadPlugin_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Plugins (*.dll)|*.dll|All Files (*.*)|*.*";
            if (ofd.ShowDialog() != DialogResult.Cancel)
                host.Project.LoadAssembly(Assembly.LoadFile(ofd.FileName), true);
        }

        IHost host;
        public override void Init(IHost host)
        {
            this.host = host;
        }
        public override void InitProj()
        {
            this.DataContext = host.Project;
            usePacker.IsChecked = host.Project.Packer != null;
        }

        private void packer_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (usePacker.IsChecked ?? false)
                host.Project.Packer = new PrjConfig<Packer>((Packer)packer.SelectedItem, host.Project);
        }

        private void usePacker_Unchecked(object sender, RoutedEventArgs e)
        {
            host.Project.Packer = null;
        }

        private void usePacker_Checked(object sender, RoutedEventArgs e)
        {
            host.Project.Packer = new PrjConfig<Packer>((Packer)packer.SelectedItem, host.Project);
        }
    }
}
