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

namespace Confuser
{
    /// <summary>
    /// Interaction logic for Database.xaml
    /// </summary>
    public partial class Database : Window, INotifyPropertyChanged
    {
        public Database()
        {
            InitializeComponent();
            this.DataContext = this;
            UpdateTitle(null);
            listView.AddHandler(Thumb.DragDeltaEvent, new DragDeltaEventHandler(Thumb_DragDelta), true);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        ObfuscationDatabase db;
        public ObfuscationDatabase Db
        {
            get { return db; }
            set
            {
                if (db != value)
                {
                    db = value;
                    if (PropertyChanged != null)
                    {
                        PropertyChanged(this, new PropertyChangedEventArgs("Db"));
                        PropertyChanged(this, new PropertyChangedEventArgs("IsDbNull"));
                    }
                }
            }
        }
        public bool IsDbNull { get { return db == null; } }

        void UpdateTitle(string dbName)
        {
            Title = "Confuser Database Viewer" + (dbName != null ? " - " + dbName : "");
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

        protected override void OnDragOver(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Copy;
            else
                e.Effects = DragDropEffects.None;
            e.Handled = true;
            base.OnDragOver(e);
        }
        protected override void OnDrop(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] file = e.Data.GetData(DataFormats.FileDrop, true) as string[];
                if (file.Length == 1)
                {
                    try
                    {
                        ObfuscationDatabase db = new ObfuscationDatabase();
                        using (BinaryReader rdr = new BinaryReader(File.OpenRead(file[0])))
                            db.Deserialize(rdr);
                        Db = db;
                        UpdateTitle(Path.GetFileName(file[0]));
                        tree.ItemsSource = new object[] { new Db(Path.GetFileName(file[0]), db) };
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(string.Format(
@"""{0}"" is not a valid database!
Message : {1}
Stack Trace : {2}", file[0], ex.Message, ex.StackTrace), "Confuser", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    return;
                }
            }
        }

        private void tree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (tree.SelectedItem is DbTable)
            {
                listView.ItemsSource = (tree.SelectedItem as DbTable).Entries;
                Filter();
            }
            else
                listView.ItemsSource = null;
        }

        private void Thumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            Thumb senderAsThumb = e.OriginalSource as Thumb;
            GridViewColumnHeader header = senderAsThumb.TemplatedParent as GridViewColumnHeader;
            if (header != null && header.Column.ActualWidth < 20)
                header.Column.Width = 20;
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            ContextMenu menu = Helper.FindParent<ContextMenu>(e.Source as DependencyObject);
            if (menu.PlacementTarget is ListViewItem)
            {
                var entry = (menu.PlacementTarget as ListViewItem).DataContext as DbEntry;
                Clipboard.SetText(entry.Name + "\t" + entry.Value);
            }
        }

        void Filter()
        {
            if (listView.ItemsSource == null) return;
            ICollectionView dataView =
              CollectionViewSource.GetDefaultView(listView.ItemsSource);

            string txt = search.Text;
            dataView.Filter = _ =>
            {
                DbEntry entry = _ as DbEntry;
                return entry.Name.IndexOf(txt, StringComparison.OrdinalIgnoreCase) != -1 || entry.Value.IndexOf(txt, StringComparison.OrdinalIgnoreCase) != -1;
            };
        }
        private void search_TextChanged(object sender, TextChangedEventArgs e)
        {
            Filter();
        }


        GridViewColumnHeader header = null;
        ListSortDirection dir = ListSortDirection.Ascending;
        void Sort(string sortBy, ListSortDirection direction)
        {
            ICollectionView dataView =
              CollectionViewSource.GetDefaultView(listView.ItemsSource);

            dataView.SortDescriptions.Clear();
            SortDescription sd = new SortDescription(sortBy, direction);
            dataView.SortDescriptions.Add(sd);
            dataView.Refresh();
        }
        private void ColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            if (listView.ItemsSource == null) return;

            GridViewColumnHeader hdr = e.OriginalSource as GridViewColumnHeader;
            ListSortDirection direction;

            if (hdr != null)
            {
                if (hdr.Role != GridViewColumnHeaderRole.Padding)
                {
                    if (hdr != this.header)
                    {
                        direction = ListSortDirection.Ascending;
                    }
                    else
                    {
                        if (dir == ListSortDirection.Ascending)
                        {
                            direction = ListSortDirection.Descending;
                        }
                        else
                        {
                            direction = ListSortDirection.Ascending;
                        }
                    }

                    string header = hdr.Column.Header as string;
                    Sort(header, direction);

                    this.header = hdr;
                    dir = direction;
                }
            }
        }
    }

    class DbEntry
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }
    class DbTable
    {
        public DbTable(string name, List<Tuple<string, string>> entries)
        {
            this.Name = name;
            this.Entries = new List<DbEntry>(entries.Count);
            foreach (var i in entries)
                this.Entries.Add(new DbEntry() { Name = i.Item1, Value = i.Item2 });
        }
        public List<DbEntry> Entries { get; private set; }
        public string Name { get; private set; }
    }
    class DbModule : List<DbTable>
    {
        public DbModule(string name, Dictionary<string, List<Tuple<string, string>>> entries)
        {
            this.Name = name;
            foreach (var i in entries)
                base.Add(new DbTable(i.Key, i.Value));
        }
        public string Name { get; private set; }
    }
    class Db : ArrayList
    {
        public string Name { get; private set; }
        public Db(string name, ObfuscationDatabase db)
        {
            this.Name = name;
            foreach (var i in db)
            {
                if (string.IsNullOrEmpty(i.Key))
                    foreach (var j in i.Value)
                        base.Add(new DbTable(j.Key, j.Value));
                else
                    base.Add(new DbModule(i.Key, i.Value));
            }
        }
    }
}
