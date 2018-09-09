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
using System.Windows.Shapes;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using Confuser.Core;

namespace Confuser
{
    /// <summary>
    /// Interaction logic for EditRule.xaml
    /// </summary>
    public partial class EditRule : Window
    {
        public PrjRule Rule { get; private set; }
        public IHost Host { get; private set; }
        public EditRule(string title, PrjRule src, IHost host)
        {
            InitializeComponent();
            Title = title;
            if (src == null) Rule = new PrjRule(null);
            else Rule = src.Clone(null);
            DataContext = Rule;
            Host = host;
            this.Loaded += (sender, e) => patternBox.Focus();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            BindingExpression exp = patternBox.GetBindingExpression(TextBox.TextProperty);
            if (exp != null)
                exp.UpdateSource();

            if (string.IsNullOrEmpty(Rule.Pattern))
            {
                MessageBox.Show("Empty pattern!");
                return;
            }
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void Bar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        //http://stackoverflow.com/questions/568012/wpf-window-drag-move-boundary
        internal enum WM
        {
            WINDOWPOSCHANGING = 0x0046,
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct WINDOWPOS
        {
            public IntPtr hwnd;
            public IntPtr hwndInsertAfter;
            public int x;
            public int y;
            public int cx;
            public int cy;
            public int flags;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            HwndSource hwndSource = (HwndSource)HwndSource.FromVisual(this);
            hwndSource.AddHook(DragHook);
            base.OnSourceInitialized(e);
        }

        private static IntPtr DragHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handeled)
        {
            switch ((WM)msg)
            {
                case WM.WINDOWPOSCHANGING:
                    {
                        WINDOWPOS pos = (WINDOWPOS)Marshal.PtrToStructure(lParam, typeof(WINDOWPOS));
                        if ((pos.flags & 0x0002) != 0)
                        {
                            return IntPtr.Zero;
                        }

                        Window wnd = (Window)HwndSource.FromHwnd(hwnd).RootVisual;
                        if (wnd == null)
                        {
                            return IntPtr.Zero;
                        }

                        bool changedPos = false;

                        Window w = wnd.Owner;
                        if (pos.x < w.Left)
                        {
                            pos.x = (int)w.Left;
                            changedPos = true;
                        }
                        if (pos.y < w.Top)
                        {
                            pos.y = (int)w.Top;
                            changedPos = true;
                        }
                        if (pos.x + pos.cx > w.Left + w.Width)
                        {
                            pos.x = (int)(w.Left + w.Width) - pos.cx;
                            changedPos = true;
                        }
                        if (pos.y + pos.cy > w.Top + w.Height)
                        {
                            pos.y = (int)(w.Top + w.Height) - pos.cy;
                            changedPos = true;
                        }

                        if (!changedPos)
                        {
                            return IntPtr.Zero;
                        }

                        Marshal.StructureToPtr(pos, lParam, true);
                        handeled = true;
                    }
                    break;
            }

            return IntPtr.Zero;
        }

        ICommand add;
        public ICommand AddCommand
        {
            get
            {
                if (add == null)
                {
                    add = new RelayCommand(_ => true, _ =>
                    {
                        Rule.Add(new PrjConfusionCfg(Host.Project.Confusions[0], Rule));
                    });
                }
                return add;
            }
        }

        ICommand remove;
        public ICommand RemoveCommand
        {
            get
            {
                if (remove == null)
                {
                    remove = new RelayCommand(_ =>
                    {
                        return Rule.Count > 0 && list.SelectedItem != null;
                    }, _ =>
                    {
                        int idx = list.SelectedIndex - 1;
                        Rule.Remove((PrjConfusionCfg)list.SelectedItem);
                        if (idx > Rule.Count) idx = Rule.Count - 1;
                        if (idx < 0) idx = 0;
                        list.SelectedIndex = idx;
                    });
                }
                return remove;
            }
        }
    }
}
