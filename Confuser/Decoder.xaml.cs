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
using Confuser.Core;
using System.ComponentModel;
using System.IO;
using System.Collections;
using System.Windows.Controls.Primitives;
using Microsoft.Win32;
using System.Text.RegularExpressions;

namespace Confuser
{
    /// <summary>
    /// Interaction logic for Database.xaml
    /// </summary>
    public partial class Decoder : Window
    {
        public Decoder()
        {
            InitializeComponent();
            Title = "Stack trace decoder";
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        private void Bar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }
        private void Bar_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            switch (this.WindowState)
            {
                case WindowState.Maximized:
                    this.WindowState = WindowState.Normal; break;
                case WindowState.Normal:
                    this.WindowState = WindowState.Maximized; break;
            }
        }

        private void path_PreviewDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
            base.OnDragOver(e);
        }
        private void path_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] file = e.Data.GetData(DataFormats.FileDrop, true) as string[];
                if (file.Length == 1)
                    path.Text = file[0];
            }
            base.OnDrop(e);
        }
        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Confuser database (*.crdb)|*.crdb|All Files (*.*)|*.*";
            if (ofd.ShowDialog() ?? false)
                path.Text = ofd.FileName;
        }

        private void Translate_Click(object sender, RoutedEventArgs e)
        {
            ObfuscationDatabase db = new ObfuscationDatabase();
            try
            {
                using (BinaryReader rdr = new BinaryReader(File.OpenRead(path.Text)))
                    db.Deserialize(rdr);
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(
@"""{0}"" is not a valid database!
Message : {1}
Stack Trace : {2}", path.Text, ex.Message, ex.StackTrace), "Confuser", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            string stacktrace = input.Text;
            var entries = new Dictionary<string, string>();
            foreach (var mod in db)
                foreach (var tbl in mod.Value)
                    if (tbl.Key == "Rename")
                        foreach (var i in tbl.Value)
                        {
                            int index = stacktrace.IndexOf(i.Item2);
                            if (index != -1 && !entries.ContainsKey(i.Item2))
                                entries.Add(i.Item2, i.Item1);
                        }

            string regex = "(" + string.Join("|", entries.Keys.Select(_ => Regex.Escape(_)).ToArray()) + ")";
            string result = Regex.Replace(stacktrace, regex, m => entries[m.Value]);

            output.Text = result;
        }

        private void Box_MouseEnter(object sender, MouseEventArgs e)
        {
            (e.Source as TextBox).Focus();
            (e.Source as TextBox).SelectAll();
        }
    }
}
