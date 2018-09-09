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
using Mono.Cecil.PE;
using System.IO;
using Mono.Cecil;
using System.Collections.ObjectModel;
using Confuser.Core;

namespace Confuser
{
    /// <summary>
    /// Interaction logic for Asms.xaml
    /// </summary>
    partial class Asms : ConfuserTab
    {
        static Asms()
        {
            TitlePropertyKey.OverrideMetadata(typeof(Asms), new UIPropertyMetadata("Assemblies"));
        }
        public Asms()
        {
            InitializeComponent();
            Style = FindResource(typeof(ConfuserTab)) as Style;
        }

        bool HasMain()
        {
            foreach (var i in host.Project.Assemblies)
            {
                if (i.IsMain) return true;
            } return false;
        }
        void DropFile(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] file = e.Data.GetData(DataFormats.FileDrop, true) as string[];
                if (file.Length == 1 && Path.GetExtension(file[0]) == ".crproj")
                {
                    host.LoadPrj(file[0]);
                    return;
                }
                foreach (var i in file)
                {
                    if (host.Project.Assemblies.Any(_ => _.Path == i)) continue;
                    using (var str = File.OpenRead(i))
                    {
                        try
                        {
                            var img = ImageReader.ReadImageFrom(str);
                            str.Position = 0;
                            var asm = new PrjAssembly(host.Project)
                            {
                                Path = i,
                                IsExecutable = img.EntryPointToken != 0,
                                Assembly = AssemblyDefinition.ReadAssembly(str)
                            };
                            if (asm.IsExecutable)
                            {
                                if (!HasMain())
                                    asm.IsMain = true;
                            }
                            if (string.IsNullOrEmpty(host.Project.OutputPath))
                                host.Project.OutputPath = Path.Combine(Path.GetDirectoryName(asm.Path), "Confused");
                            host.Project.Assemblies.Add(asm);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(string.Format(
@"""{0}"" is not a valid assembly!
Message : {1}
Stack Trace : {2}", i, ex.Message, ex.StackTrace), "Confuser", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
        }

        private void view_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && view.SelectedItems.Count > 0)
            {
                var items = view.SelectedItems.OfType<PrjAssembly>().ToArray();
                int selIdx = view.SelectedIndex;

                StringBuilder msg = new StringBuilder();
                msg.AppendLine("Are you sure you remove the following assemblies?");
                foreach (var i in items)
                    msg.AppendLine(i.Path);
                msg.AppendLine("All settings on it will be discarded!");

                if (MessageBox.Show(msg.ToString(), "Confuser", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    foreach (var i in items)
                        host.Project.Assemblies.Remove(i);
                    if (items.Length == 1)
                    {
                        if (selIdx < host.Project.Assemblies.Count)
                            view.SelectedIndex = selIdx;
                        else
                            view.SelectedIndex = host.Project.Assemblies.Count - 1;
                    }
                    else
                        view.SelectedIndex = host.Project.Assemblies.Count - 1;
                }
            }
        }

        IHost host;
        public override void Init(IHost host)
        {
            this.host = host;
            (host as Window).DragOver += (sender, e) =>
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                    e.Effects = DragDropEffects.Copy;
                else
                    e.Effects = DragDropEffects.None;
                e.Handled = true;
            };
            (host as Window).Drop += DropFile;
        }
        public override void InitProj()
        {
            view.ItemsSource = host.Project.Assemblies;
        }

        private void view_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var asm = (PrjAssembly)view.SelectedItem;
            if (asm == null) info.DataContext = null;
            else
                info.DataContext = new
                {
                    Icon = Helper.GetIcon(asm.Path),
                    Filename = asm.Path,
                    Fullname = asm.Assembly.FullName
                };
        }
    }
}
