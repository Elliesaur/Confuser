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
using System.ComponentModel;
using Mono.Cecil;

namespace Confuser
{
    /// <summary>
    /// Interaction logic for Simple.xaml
    /// </summary>
    public partial class Rules : ConfuserTab, IPage
    {
        static Rules()
        {
            TitlePropertyKey.OverrideMetadata(typeof(Rules), new UIPropertyMetadata("Rules"));
        }
        public Rules()
        {
            InitializeComponent();
            Style = FindResource(typeof(ConfuserTab)) as Style;

        }
        CollectionViewSource src;

        IHost host;
        PrjPreset prevPreset;
        public override void Init(IHost host)
        {
            this.host = host;
        }
        public override void InitProj()
        {
            rulesList.ItemsSource = host.Project.Rules;

            this.DataContext = host.Project;
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
                        var win = new EditRule("Add rule", null, host) { Owner = host as Window };
                        if (win.ShowDialog() ?? false)
                            host.Project.Rules.Add(win.Rule.Clone(host.Project));
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
                        return host.Project.Rules.Count > 0 && rulesList.SelectedItem != null;
                    }, _ =>
                    {
                        var rules = host.Project.Rules;
                        int idx = rulesList.SelectedIndex - 1;
                        rules.Remove((PrjRule)rulesList.SelectedItem);
                        if (idx > rules.Count) idx = rules.Count - 1;
                        if (idx < 0) idx = 0;
                        rulesList.SelectedIndex = idx;
                    });
                }
                return remove;
            }
        }

        ICommand moveUp;
        public ICommand MoveUpCommand
        {
            get
            {
                if (moveUp == null)
                {
                    moveUp = new RelayCommand(_ =>
                    {
                        return host.Project.Rules.Count > 0 && rulesList.SelectedIndex > 0;
                    }, _ =>
                    {
                        var rules = host.Project.Rules;
                        int idx = rulesList.SelectedIndex;
                        PrjRule x = rules[idx];
                        rules.RemoveAt(idx);
                        rules.Insert(idx - 1, x);
                        rulesList.SelectedIndex = idx - 1;
                    });
                }
                return moveUp;
            }
        }

        ICommand moveDown;
        public ICommand MoveDownCommand
        {
            get
            {
                if (moveDown == null)
                {
                    moveDown = new RelayCommand(_ =>
                    {
                        return host.Project.Rules.Count > 0 &&
                               rulesList.SelectedIndex >= 0 &&
                               rulesList.SelectedIndex < host.Project.Rules.Count - 1;
                    }, _ =>
                    {
                        var rules = host.Project.Rules;
                        int idx = rulesList.SelectedIndex;
                        PrjRule x = rules[idx];
                        rules.RemoveAt(idx);
                        rules.Insert(idx + 1, x);
                        rulesList.SelectedIndex = idx + 1;
                    });
                }
                return moveDown;
            }
        }

        private void rulesList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var elem = rulesList.InputHitTest(e.GetPosition(rulesList));
            ListBoxItem item = Helper.FindParent<ListBoxItem>(elem as DependencyObject);
            if (item != null)
            {
                PrjRule rule = item.Content as PrjRule;
                var win = new EditRule("Edit rule", rule, host) { Owner = host as Window };
                if (win.ShowDialog() ?? false)
                {
                    int idx = host.Project.Rules.IndexOf(rule);
                    host.Project.Rules.RemoveAt(idx);
                    host.Project.Rules.Insert(idx, win.Rule.Clone(host.Project));
                    rulesList.SelectedIndex = idx;
                }
            }
        }
    }
}
