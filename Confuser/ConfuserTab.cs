using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows;

namespace Confuser
{
    public /*abstract*/ class ConfuserTab : Grid, IPage // =( my lovely abstract killed by wpf designer
    {
        public string Title
        {
            get { return (string)GetValue(TitleProperty); }
        }
        protected static readonly DependencyPropertyKey TitlePropertyKey = DependencyProperty.RegisterReadOnly("Title", typeof(string), typeof(ConfuserTab), new UIPropertyMetadata(">.<"));
        public static readonly DependencyProperty TitleProperty = TitlePropertyKey.DependencyProperty;

        public /*abstract*/ virtual void Init(IHost host) { }
        public /*abstract*/ virtual void InitProj() { }
        public /*abstract*/ virtual void OnActivated() { }
    }
}
