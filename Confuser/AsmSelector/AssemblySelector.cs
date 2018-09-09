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
using System.ComponentModel;
using System.Collections.ObjectModel;
using Mono.Cecil;
using System.Reflection;

namespace Confuser.AsmSelector
{
    public class AssemblySelector : TreeView
    {
        static AssemblySelector()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(AssemblySelector), new FrameworkPropertyMetadata(typeof(AssemblySelector)));
        }

        public void AddAssembly(AssemblyDefinition asmDef)
        {
            Items.Add(new AsmTreeModel(asmDef));
        }

        public void ClearSelection()
        {
            if (base.SelectedItem != null)
                (base.SelectedItem as AsmTreeModel).IsSelected = false;
            FocusManager.SetFocusedElement(FocusManager.GetFocusScope(this), this);
        }

        public new IAnnotationProvider SelectedItem { get { if (base.SelectedItem != null) return (IAnnotationProvider)(base.SelectedItem as AsmTreeModel).Object; else return null; } }
    }

    class AsmTreeModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged(string prop)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
        }

        object obj = null;
        public object Object
        {
            get { return obj; }
            set
            {
                if (obj != value)
                {
                    obj = value;
                    OnPropertyChanged("Object");
                }
            }
        }

        static AsmTreeModel dummy = new AsmTreeModel(null);
        bool hasChildren;
        public AsmTreeModel(object obj)
        {
            this.obj = obj;
            hasChildren = Childer.HasChildren(obj);
            if (hasChildren)
                children.Add(dummy);
        }

        bool expand = false;
        public bool IsExpanded
        {
            get { return expand; }
            set
            {
                if (expand != value)
                {
                    expand = value;
                    this.OnPropertyChanged("IsExpanded");
                }

                if (hasChildren && children.Contains(dummy))
                {
                    this.Children.Remove(dummy);
                    Childer.AddChildren(obj, this.Children);
                }
            }
        }

        bool select = false;
        public bool IsSelected
        {
            get { return select; }
            set
            {
                if (select != value)
                {
                    select = value;
                    this.OnPropertyChanged("IsSelected");
                }
            }
        }

        ObservableCollection<AsmTreeModel> children = new ObservableCollection<AsmTreeModel>();
        public ObservableCollection<AsmTreeModel> Children { get { return children; } }
    }

    class AsmTextConverter : IValueConverter
    {
        public static AsmTextConverter Instance = new AsmTextConverter();

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return Texter.Text(value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    class AsmColorConverter : IValueConverter
    {
        public static AsmColorConverter Instance = new AsmColorConverter();

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return Colorizer.Colorize(value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
