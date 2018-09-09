using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Xml;
using Confuser.Core;
using Confuser.Core.Project;
using Microsoft.Win32;
using WPF.JoshSmith.Adorners;

namespace Confuser
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IHost
    {
        static string VerStr;
        static MainWindow()
        {
            VerStr = "Panfuser v" + typeof(Core.Confuser).Assembly.GetName().Version.ToString();
        }
        public MainWindow()
        {
            InitializeComponent();
            this.Width = 800; this.Height = 600;

            //=_=||
            Tab.ApplyTemplate();
            TabPanel panel = Tab.Template.FindName("HeaderPanel", Tab) as TabPanel;
            panel.SetBinding(TabPanel.IsEnabledProperty, new Binding() { Path = new PropertyPath(EnabledNavigationProperty), Source = this });

            menu = FindResource("dropMenu") as ContextMenu;
            menu.PlacementTarget = drop;
            menu.Placement = PlacementMode.Bottom;
            (menu.Items[0] as MenuItem).Click += DbViewer_Click;
            (menu.Items[1] as MenuItem).Click += StackDecoder_Click;
            (menu.Items[2] as MenuItem).Click += About_Click;

            IPage page;

            page = new Asms();
            page.Init(this);
            Tab.Items.Add(page);

            page = new Settings();
            page.Init(this);
            Tab.Items.Add(page);

            page = new Rules();
            page.Init(this);
            Tab.Items.Add(page);

            page = new Progress();
            page.Init(this);
            Tab.Items.Add(page);

            Project = new Prj();
            foreach (ConfuserTab i in Tab.Items)
                i.InitProj();
            Project.PropertyChanged += new PropertyChangedEventHandler(ProjectChanged);
            ProjectChanged(Project, new PropertyChangedEventArgs(""));
        }

        public bool EnabledNavigation
        {
            get { return (bool)GetValue(EnabledNavigationProperty); }
            set { SetValue(EnabledNavigationProperty, value); }
        }
        public static readonly DependencyProperty EnabledNavigationProperty =
            DependencyProperty.Register("EnabledNavigation", typeof(bool), typeof(MainWindow), new UIPropertyMetadata(true, EnabledNavigationChanged));

        static void EnabledNavigationChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            (sender as MainWindow).AllowDrop = (bool)e.NewValue;
        }

        public Prj Project { get; private set; }

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
        public override void OnApplyTemplate()
        {
            System.IntPtr handle = (new WindowInteropHelper(this)).Handle;
            HwndSource.FromHwnd(handle).AddHook(new HwndSourceHook(WindowProc));
        }

        private void Tab_MouseDown(object sender, MouseButtonEventArgs e)
        {
            TabItem tab = e.OriginalSource as TabItem ?? Helper.FindParent<TabItem>((DependencyObject)e.OriginalSource);
            if (tab != null)
            {
                tab.IsSelected = true;
                e.Handled = true;
            }
        }

        public void LoadPrj(string path)
        {
            if (Project.IsModified)
            {
                switch (MessageBox.Show(
                    "You have unsaved changes in this project!\r\nDo you want to save them?",
                    "Confuser", MessageBoxButton.YesNoCancel, MessageBoxImage.Question))
                {
                    case MessageBoxResult.Yes:
                        Save_Click(this, new RoutedEventArgs());
                        break;
                    case MessageBoxResult.No:
                        break;
                    case MessageBoxResult.Cancel:
                        return;
                }
            }

            try
            {
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(path);

                ConfuserProject proj = new ConfuserProject();
                proj.Load(xmlDoc);

                Prj prj = new Prj();
                prj.FileName = path;
                prj.FromConfuserProject(proj);

                Project = prj;
                foreach (ConfuserTab i in Tab.Items)
                    i.InitProj();
                prj.PropertyChanged += new PropertyChangedEventHandler(ProjectChanged);
                prj.IsModified = false;
                ProjectChanged(Project, new PropertyChangedEventArgs(""));
                Tab.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(
@"Invalid project file!
Message : {0}
Stack Trace : {1}", ex.Message, ex.StackTrace), "Confuser", MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }
        private void New_Click(object sender, RoutedEventArgs e)
        {
            if (Project.IsModified)
            {
                switch (MessageBox.Show(
                    "You have unsaved changes in this project!\r\nDo you want to save them?",
                    "Confuser", MessageBoxButton.YesNoCancel, MessageBoxImage.Question))
                {
                    case MessageBoxResult.Yes:
                        Save_Click(this, new RoutedEventArgs());
                        break;
                    case MessageBoxResult.No:
                        break;
                    case MessageBoxResult.Cancel:
                        return;
                }
            }

            Project = new Prj();
            foreach (ConfuserTab i in Tab.Items)
                i.InitProj();

            Project = new Prj();
            foreach (ConfuserTab i in Tab.Items)
                i.InitProj();
            Project.PropertyChanged += new PropertyChangedEventHandler(ProjectChanged);
            ProjectChanged(Project, new PropertyChangedEventArgs(""));
            Tab.SelectedIndex = 0;
        }
        private void Open_Click(object sender, RoutedEventArgs e)
        {
            if (Project.IsModified)
            {
                switch (MessageBox.Show(
                    "You have unsaved changes in this project!\r\nDo you want to save them?",
                    "Confuser", MessageBoxButton.YesNoCancel, MessageBoxImage.Question))
                {
                    case MessageBoxResult.Yes:
                        Save_Click(this, new RoutedEventArgs());
                        break;
                    case MessageBoxResult.No:
                        break;
                    case MessageBoxResult.Cancel:
                        return;
                }
            }

            OpenFileDialog sfd = new OpenFileDialog();
            sfd.Filter = "Confuser Project (*.crproj)|*.crproj|All Files (*.*)|*.*";
            if (sfd.ShowDialog() ?? false)
            {
                try
                {
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.Load(sfd.FileName);

                    ConfuserProject proj = new ConfuserProject();
                    proj.Load(xmlDoc);

                    Prj prj = new Prj();
                    prj.FromConfuserProject(proj);
                    prj.FileName = sfd.FileName;

                    Project = prj;
                    foreach (ConfuserTab i in Tab.Items)
                        i.InitProj();
                    prj.PropertyChanged += new PropertyChangedEventHandler(ProjectChanged);
                    prj.IsModified = false;
                    ProjectChanged(Project, new PropertyChangedEventArgs(""));
                    Tab.SelectedIndex = 0;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format(
    @"Invalid project file!
Message : {0}
Stack Trace : {1}", ex.Message, ex.StackTrace), "Confuser", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (Project.FileName == null)
            {
                SaveFileDialog sfd = new SaveFileDialog();
                sfd.Filter = "Confuser Project (*.crproj)|*.crproj|All Files (*.*)|*.*";
                if (sfd.ShowDialog() ?? false)
                {
                    ConfuserProject proj = Project.ToCrProj();
                    XmlWriterSettings wtrSettings = new XmlWriterSettings();
                    wtrSettings.Indent = true;
                    XmlWriter writer = XmlWriter.Create(sfd.FileName, wtrSettings);
                    proj.Save().Save(writer);
                    Project.IsModified = false;
                    Project.FileName = sfd.FileName;
                    ProjectChanged(proj, new PropertyChangedEventArgs(""));
                }
            }
            else
            {
                ConfuserProject proj = Project.ToCrProj();
                proj.Save().Save(Project.FileName);
                Project.IsModified = false;
                ProjectChanged(proj, new PropertyChangedEventArgs(""));
            }
        }
        ContextMenu menu;
        private void Drop_Click(object sender, RoutedEventArgs e)
        {
            menu.IsOpen = true;
        }

        void DbViewer_Click(object sender, RoutedEventArgs e)
        {
            new Database().ShowDialog();
        }
        void StackDecoder_Click(object sender, RoutedEventArgs e)
        {
            new Decoder().ShowDialog();
        }
        void About_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(VerStr + " developed by Ki!", "Confuser", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (Project.IsModified)
            {
                switch (MessageBox.Show(
                    "You have unsaved changes in this project!\r\nDo you want to save them?",
                    "Confuser", MessageBoxButton.YesNoCancel, MessageBoxImage.Question))
                {
                    case MessageBoxResult.Yes:
                        Save_Click(this, new RoutedEventArgs());
                        break;
                    case MessageBoxResult.No:
                        break;
                    case MessageBoxResult.Cancel:
                        e.Cancel = true;
                        break;
                }
            }
            base.OnClosing(e);
        }

        void ProjectChanged(object sender, PropertyChangedEventArgs e)
        {
            Title = string.Format("{0} - {1} {2}",
                VerStr,
                Path.GetFileName(Project.FileName ?? "Untitled.crproj"),
                Project.IsModified ? "*" : "");
        }

        int tabSel;
        private void Tab_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.OriginalSource == Tab)
            {
                if (Tab.SelectedIndex != Tab.Items.Count - 1)
                {
                    tabSel = Tab.SelectedIndex;
                    FocusManager.SetFocusedElement(FocusManager.GetFocusScope(Tab), Tab.SelectedItem as ConfuserTab);
                    Dispatcher.BeginInvoke(new Action((Tab.SelectedItem as ConfuserTab).OnActivated), System.Windows.Threading.DispatcherPriority.Loaded);
                }
                else if (Tab.SelectedIndex != tabSel)
                {
                    if (Project.Assemblies.Count == 0)
                        MessageBox.Show("No input assemblies!", "Confuser", MessageBoxButton.OK, MessageBoxImage.Warning);
                    else if (string.IsNullOrEmpty(Project.OutputPath))
                        MessageBox.Show("No output path specified!", "Confuser", MessageBoxButton.OK, MessageBoxImage.Warning);
                    else
                    {
                        tabSel = Tab.SelectedIndex;
                        FocusManager.SetFocusedElement(FocusManager.GetFocusScope(Tab), Tab.SelectedItem as ConfuserTab);
                        Dispatcher.BeginInvoke(new Action((Tab.SelectedItem as ConfuserTab).OnActivated), System.Windows.Threading.DispatcherPriority.Loaded);
                        return;
                    }
                    e.Handled = true;
                    Tab.SelectedIndex = tabSel;
                }

            }
        }



        private static System.IntPtr WindowProc(
              System.IntPtr hwnd,
              int msg,
              System.IntPtr wParam,
              System.IntPtr lParam,
              ref bool handled)
        {
            switch (msg)
            {
                case 0x0024:/* WM_GETMINMAXINFO */
                    WmGetMinMaxInfo(hwnd, lParam);
                    handled = true;
                    break;
            }

            return (System.IntPtr)0;
        }

        private static void WmGetMinMaxInfo(System.IntPtr hwnd, System.IntPtr lParam)
        {

            MINMAXINFO mmi = (MINMAXINFO)Marshal.PtrToStructure(lParam, typeof(MINMAXINFO));

            // Adjust the maximized size and position to fit the work area of the correct monitor
            int MONITOR_DEFAULTTONEAREST = 0x00000002;
            System.IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);

            if (monitor != System.IntPtr.Zero)
            {

                MONITORINFO monitorInfo = new MONITORINFO();
                GetMonitorInfo(monitor, monitorInfo);
                RECT rcWorkArea = monitorInfo.rcWork;
                RECT rcMonitorArea = monitorInfo.rcMonitor;
                mmi.ptMaxPosition.x = Math.Abs(rcWorkArea.left - rcMonitorArea.left);
                mmi.ptMaxPosition.y = Math.Abs(rcWorkArea.top - rcMonitorArea.top);
                mmi.ptMaxSize.x = Math.Abs(rcWorkArea.right - rcWorkArea.left);
                mmi.ptMaxSize.y = Math.Abs(rcWorkArea.bottom - rcWorkArea.top);
            }

            Marshal.StructureToPtr(mmi, lParam, true);
        }


        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;

            public POINT(int x, int y)
            {
                this.x = x;
                this.y = y;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        };

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public class MONITORINFO
        {
            public int cbSize = Marshal.SizeOf(typeof(MONITORINFO));

            public RECT rcMonitor = new RECT();

            public RECT rcWork = new RECT();

            public int dwFlags = 0;
        }


        [StructLayout(LayoutKind.Sequential, Pack = 0)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;

            public static readonly RECT Empty = new RECT();

            public int Width
            {
                get { return Math.Abs(right - left); }
            }
            public int Height
            {
                get { return bottom - top; }
            }

            public RECT(int left, int top, int right, int bottom)
            {
                this.left = left;
                this.top = top;
                this.right = right;
                this.bottom = bottom;
            }


            public RECT(RECT rcSrc)
            {
                this.left = rcSrc.left;
                this.top = rcSrc.top;
                this.right = rcSrc.right;
                this.bottom = rcSrc.bottom;
            }

            public bool IsEmpty
            {
                get
                {
                    return left >= right || top >= bottom;
                }
            }
            public override string ToString()
            {
                if (this == RECT.Empty) { return "RECT {Empty}"; }
                return "RECT { left : " + left + " / top : " + top + " / right : " + right + " / bottom : " + bottom + " }";
            }

            public override bool Equals(object obj)
            {
                if (!(obj is Rect)) { return false; }
                return (this == (RECT)obj);
            }

            public override int GetHashCode()
            {
                return left.GetHashCode() + top.GetHashCode() + right.GetHashCode() + bottom.GetHashCode();
            }


            public static bool operator ==(RECT rect1, RECT rect2)
            {
                return (rect1.left == rect2.left && rect1.top == rect2.top && rect1.right == rect2.right && rect1.bottom == rect2.bottom);
            }

            public static bool operator !=(RECT rect1, RECT rect2)
            {
                return !(rect1 == rect2);
            }


        }

        [DllImport("user32")]
        internal static extern bool GetMonitorInfo(IntPtr hMonitor, MONITORINFO lpmi);
        [DllImport("User32")]
        internal static extern IntPtr MonitorFromWindow(IntPtr handle, int flags);
    }

    public interface IHost
    {
        bool EnabledNavigation { get; set; }
        Prj Project { get; }
        void LoadPrj(string path);
    }

    public interface IPage
    {
        void Init(IHost host);
        void InitProj();
    }
}
