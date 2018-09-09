using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Mono.Cecil.PE;
using System.IO;
using Mono.Cecil;
using System.Collections.ObjectModel;

namespace Confuser
{
    /// <summary>
    /// Interaction logic for Start.xaml
    /// </summary>
    partial class Start : Page, IPage<object>
    {
        public Start()
        {
            InitializeComponent();
            Style = FindResource(typeof(Page)) as Style;
        }

        protected override void OnDrop(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                host.Go(new LoadAsm(), e);
            }
        }

        IHost host;
        public void Init(IHost host, object parameter)
        {
            this.host = host;
        }

        private void loadAsm_Click(object sender, RoutedEventArgs e)
        {
            host.Go(new LoadAsm(), false);
        }

        private void openPrj_Click(object sender, RoutedEventArgs e)
        {

        }

        private void declObf_Click(object sender, RoutedEventArgs e)
        {
            host.Go(new LoadAsm(), true);
        }
    }
}
