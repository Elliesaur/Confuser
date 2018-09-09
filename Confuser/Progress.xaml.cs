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
using Confuser.Core;
using System.Threading;
using Mono.Cecil;
using System.Windows.Media.Imaging;
using System.IO;
using System.Security;

namespace Confuser
{
    /// <summary>
    /// Interaction logic for Progress.xaml
    /// </summary>
    public partial class Progress : ConfuserTab, IPage
    {
        static Progress()
        {
            TitlePropertyKey.OverrideMetadata(typeof(Progress), new UIPropertyMetadata("Confuse!"));
        }
        public Progress()
        {
            InitializeComponent();
            Style = FindResource(typeof(ConfuserTab)) as Style;
        }

        Core.Confuser cr;
        Thread thread;

        IHost host;
        public override void Init(IHost host)
        {
            this.host = host;
        }

        class AsmData
        {
            public AssemblyDefinition Assembly { get; set; }
            public BitmapSource Icon { get; set; }
            public string Filename { get; set; }
            public string Fullname { get; set; }
        }

        void Begin()
        {
            var parameter = new ConfuserParameter();
            parameter.Project = host.Project.ToCrProj();
            parameter.Logger.BeginAssembly += Logger_BeginAssembly;
            parameter.Logger.EndAssembly += Logger_EndAssembly;
            parameter.Logger.Phase += Logger_Phase;
            parameter.Logger.Log += Logger_Log;
            parameter.Logger.Progress += Logger_Progress;
            parameter.Logger.Fault += Logger_Fault;
            parameter.Logger.End += Logger_End;

            cr = new Confuser.Core.Confuser();
            thread = cr.ConfuseAsync(parameter);
            host.EnabledNavigation = false;
            btn.IsEnabled = true;

            p = 0;
            Action check = null;
            check = new Action(() =>
            {
                progress.Value = p;
                if (p != -1)
                    Dispatcher.BeginInvoke(check, System.Windows.Threading.DispatcherPriority.Background);
            });
            check();
        }

        public override void OnActivated()
        {
            base.OnActivated();
            log.Clear();
            Begin();
        }

        void Logger_End(object sender, LogEventArgs e)
        {
            if (!CheckAccess())
            {
                Dispatcher.Invoke(new EventHandler<LogEventArgs>(Logger_End), sender, e);
                return;
            }
            asmLbl.DataContext = new AsmData()
            {
                Assembly = null,
                Icon = (BitmapSource)FindResource("ok"),
                Filename = "Success!",
                Fullname = e.Message
            };
            log.AppendText(e.Message + "\r\n");

            progress.Value = 10000;

            cr = null;
            thread = null;
            btn.IsEnabled = false;
            host.EnabledNavigation = true;
            p = -1;
            Dispatcher.BeginInvoke(new Action(() => GC.Collect()), System.Windows.Threading.DispatcherPriority.SystemIdle);
        }
        void Logger_Fault(object sender, ExceptionEventArgs e)
        {
            if (!CheckAccess())
            {
                Dispatcher.Invoke(new EventHandler<ExceptionEventArgs>(Logger_Fault), sender, e);
                return;
            }
            asmLbl.DataContext = new AsmData()
            {
                Assembly = null,
                Icon = (BitmapSource)FindResource("error"),
                Filename = "Failure!",
                Fullname = e.Exception is ThreadAbortException ? "Cancelled." : e.Exception.Message
            };
            if (e.Exception is ThreadAbortException)
            {
                log.AppendText("Cancelled!\r\n");
            }
            else if (
                e.Exception is SecurityException ||
                e.Exception is DirectoryNotFoundException ||
                e.Exception is UnauthorizedAccessException ||
                e.Exception is IOException)
            {
                log.AppendText("\r\n\r\n\r\n");
                log.AppendText("Oops... Confuser crashed...\r\n");
                log.AppendText("\r\n");
                log.AppendText(e.Exception.GetType().FullName + "\r\n");
                log.AppendText("Message : " + e.Exception.Message + "\r\n");
                log.AppendText("Stack Trace :\r\n");
                log.AppendText(e.Exception.StackTrace + "\r\n");
                log.AppendText("\r\n");
                log.AppendText("Please ensure Confuser have enough permission!!!\r\n");
            }
            else
            {
                log.AppendText("\r\n\r\n\r\n");
                log.AppendText("Oops... Confuser crashed...\r\n");
                log.AppendText("\r\n");
                log.AppendText(e.Exception.GetType().FullName + "\r\n");
                log.AppendText("Message : " + e.Exception.Message + "\r\n");
                log.AppendText("Stack Trace :\r\n");
                log.AppendText(e.Exception.StackTrace + "\r\n");
                log.AppendText("\r\n");
                log.AppendText("Please report it!!!\r\n");
            }

            cr = null;
            thread = null;
            btn.IsEnabled = false;
            host.EnabledNavigation = true;
            p = -1;
            Dispatcher.BeginInvoke(new Action(() => GC.Collect()), System.Windows.Threading.DispatcherPriority.SystemIdle);
        }
        double p;
        void Logger_Progress(object sender, ProgressEventArgs e)
        {
            if (e.Progress == 0) p = 0;
            else
                p = e.Progress * 10000 / e.Total;
        }
        void Logger_Log(object sender, LogEventArgs e)
        {
            if (!CheckAccess())
            {
                Dispatcher.BeginInvoke(new EventHandler<LogEventArgs>(Logger_Log), sender, e);
                return;
            }
            log.AppendText(e.Message + "\r\n");
            log.ScrollToEnd();
        }
        void Logger_Phase(object sender, LogEventArgs e)
        {
            if (!CheckAccess())
            {
                Dispatcher.BeginInvoke(new EventHandler<LogEventArgs>(Logger_Phase), sender, e);
                return;
            }
            asmLbl.DataContext = new AsmData()
            {
                Assembly = null,
                Icon = (BitmapSource)FindResource("loading"),
                Filename = e.Message,
                Fullname = e.Message
            };
            log.AppendText("\r\n");
        }
        void Logger_EndAssembly(object sender, AssemblyEventArgs e)
        {
            if (!CheckAccess())
            {
                Dispatcher.BeginInvoke(new EventHandler<AssemblyEventArgs>(Logger_EndAssembly), sender, e);
                return;
            }
        }
        void Logger_BeginAssembly(object sender, AssemblyEventArgs e)
        {
            if (!CheckAccess())
            {
                Dispatcher.BeginInvoke(new EventHandler<AssemblyEventArgs>(Logger_BeginAssembly), sender, e);
                return;
            }
            asmLbl.DataContext = new AsmData()
            {
                Assembly = e.Assembly,
                Icon = Helper.GetIcon(e.Assembly.MainModule.FullyQualifiedName),
                Filename = e.Assembly.MainModule.FullyQualifiedName,
                Fullname = e.Assembly.FullName
            };
            log.AppendText("\r\n");
            log.ScrollToEnd();
        }


        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (thread != null)
                thread.Abort();
        }
    }
}
