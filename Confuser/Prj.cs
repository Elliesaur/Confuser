using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Confuser.Core;
using System.Reflection;
using Mono.Cecil;
using System.Windows;
using System.ComponentModel;
using Confuser.Core.Project;

namespace Confuser
{
    public interface INotifyChildrenChanged : INotifyPropertyChanged
    {
        INotifyChildrenChanged Parent { get; }
        void OnChildChanged();
    }

    public class PrjArgument
    {
        string name;
        public string Name
        {
            get { return name; }
            set
            {
                if (name != value)
                {
                    name = value;
                    if (parent != null) parent.OnChildChanged();
                }
            }
        }
        string val;
        public string Value
        {
            get { return val; }
            set
            {
                if (val != value)
                {
                    val = value;
                    if (parent != null) parent.OnChildChanged();
                }
            }
        }

        INotifyChildrenChanged parent;
        public PrjArgument(INotifyChildrenChanged parent)
        {
            this.parent = parent;
        }
    }

    public class PrjConfig<T> : ObservableCollection<PrjArgument>, INotifyChildrenChanged
    {
        INotifyChildrenChanged parent;
        public INotifyChildrenChanged Parent { get { return parent; } }
        public PrjConfig(T obj, INotifyChildrenChanged parent)
        {
            this.obj = obj;
            this.parent = parent;
        }

        T obj;
        public T Object
        {
            get { return obj; }
            set
            {
                if (!object.ReferenceEquals(obj, value))
                {
                    obj = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("Object"));
                }
            }
        }

        SettingItemAction action;
        public SettingItemAction Action
        {
            get { return action; }
            set
            {
                if (action != value)
                {
                    action = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("Action"));
                }
            }
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (parent != null) parent.OnChildChanged();
            base.OnPropertyChanged(e);
        }
        public void OnChildChanged()
        {
            if (parent != null) parent.OnChildChanged();
        }

        public SettingItem<T> ToCrConfig()
        {
            SettingItem<T> ret = new SettingItem<T>();
            if (Object is Packer)
                ret.Id = (Object as Packer).ID;
            else
                ret.Id = (Object as IConfusion).ID;
            ret.Action = action;
            foreach (var i in this)
                ret.Add(i.Name, i.Value);
            return ret;
        }
    }

    public class PrjConfusionCfg : PrjConfig<IConfusion>   //Wrapper for XAML
    {
        public PrjConfusionCfg(IConfusion obj, INotifyChildrenChanged parent) : base(obj, parent) { }
    }

    public class PrjRule : ObservableCollection<PrjConfusionCfg>, INotifyChildrenChanged
    {
        INotifyChildrenChanged parent;
        public INotifyChildrenChanged Parent { get { return parent; } }
        public PrjRule(INotifyChildrenChanged parent)
        {
            this.parent = parent;
        }

        Preset preset = Preset.None;
        public Preset Preset
        {
            get { return preset; }
            set
            {
                if (preset != value)
                {
                    preset = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("Preset"));
                }
            }
        }

        string pattern = ".*";
        public string Pattern
        {
            get { return pattern; }
            set
            {
                if (pattern != value)
                {
                    pattern = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("Pattern"));
                }
            }
        }

        bool inherit = true;
        public bool Inherit
        {
            get { return inherit; }
            set
            {
                if (inherit != value)
                {
                    inherit = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("Inherit"));
                }
            }
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (parent != null) parent.OnChildChanged();
            base.OnPropertyChanged(e);
        }
        public void OnChildChanged()
        {
            if (parent != null)
                parent.OnChildChanged();
        }

        public PrjRule Clone(INotifyChildrenChanged parent)
        {
            PrjRule ret = new PrjRule(parent);
            ret.inherit = inherit;
            ret.preset = preset;
            ret.pattern = pattern;
            foreach (PrjConfusionCfg i in this)
            {
                PrjConfusionCfg n = new PrjConfusionCfg(i.Object, ret);
                n.Action = i.Action;
                foreach (var j in i)
                    n.Add(new PrjArgument(n) { Name = j.Name, Value = j.Value });
                ret.Add(n);
            }
            return ret;
        }

        public Rule ToCrRule()
        {
            Rule ret = new Rule();
            ret.Preset = preset;
            ret.Pattern = pattern;
            ret.Inherit = inherit;
            foreach (var i in this)
                ret.Add(i.ToCrConfig());
            return ret;
        }
        public void FromCrRule(Prj prj, Rule rule)
        {
            pattern = rule.Pattern;
            preset = rule.Preset;
            inherit = rule.Inherit;
            foreach (var i in rule)
            {
                PrjConfusionCfg cfg = new PrjConfusionCfg(prj.Confusions.Single(_ => _.ID == i.Id), this);
                cfg.Action = i.Action;
                foreach (var j in i.AllKeys)
                    cfg.Add(new PrjArgument(this) { Name = j, Value = i[j] });
                this.Add(cfg);
            }
        }
    }
    public class PrjAssembly : INotifyPropertyChanged
    {
        INotifyChildrenChanged parent;
        public INotifyChildrenChanged Parent { get { return parent; } }
        public PrjAssembly(INotifyChildrenChanged parent)
        {
            this.parent = parent;
        }

        AssemblyDefinition asmDef;
        public AssemblyDefinition Assembly
        {
            get { return asmDef; }
            set
            {
                if (asmDef != value)
                {
                    asmDef = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("Assembly"));
                }
            }
        }
        string path;
        public string Path
        {
            get { return path; }
            set
            {
                if (path != value)
                {
                    path = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("Path"));
                }
            }
        }
        bool isMain;
        public bool IsMain
        {
            get { return isMain; }
            set
            {
                if (isMain != value)
                {
                    isMain = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("IsMain"));
                }
            }
        }

        public bool IsExecutable { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (parent != null) parent.OnChildChanged();
            if (PropertyChanged != null)
                PropertyChanged(this, e);
        }

        public ProjectAssembly ToCrAssembly()
        {
            ProjectAssembly ret = new ProjectAssembly();
            ret.Path = path;
            ret.IsMain = isMain;
            return ret;
        }
        public void FromCrAssembly(Prj prj, ProjectAssembly asm)
        {
            this.path = asm.Path;
            this.asmDef = asm.Resolve(prj.GetBasePath());
            this.IsExecutable = this.asmDef.MainModule.EntryPoint != null;
            this.isMain = asm.IsMain;
        }

    }

    public enum PrjPreset
    {
        None,
        Minimum,
        Normal,
        Aggressive,
        Maximum,
        Undefined
    }
    public class Prj : INotifyChildrenChanged
    {
        static Prj()
        {
            foreach (Type type in typeof(IConfusion).Assembly.GetTypes())
            {
                if (typeof(IConfusion).IsAssignableFrom(type) && type != typeof(IConfusion))
                    DefaultConfusions.Add(Activator.CreateInstance(type) as IConfusion);
                if (typeof(Packer).IsAssignableFrom(type) && type != typeof(Packer))
                    DefaultPackers.Add(Activator.CreateInstance(type) as Packer);
            }
            for (int i = 0; i < DefaultConfusions.Count; i++)
                for (int j = i; j < DefaultConfusions.Count; j++)
                    if (Comparer<string>.Default.Compare(DefaultConfusions[i].Name, DefaultConfusions[j].Name) > 0)
                    {
                        var tmp = DefaultConfusions[i];
                        DefaultConfusions[i] = DefaultConfusions[j];
                        DefaultConfusions[j] = tmp;
                    }
            for (int i = 0; i < DefaultPackers.Count; i++)
                for (int j = i; j < DefaultPackers.Count; j++)
                    if (Comparer<string>.Default.Compare(DefaultPackers[i].Name, DefaultPackers[j].Name) > 0)
                    {
                        var tmp = DefaultPackers[i];
                        DefaultPackers[i] = DefaultPackers[j];
                        DefaultPackers[j] = tmp;
                    }
        }

        public static readonly ObservableCollection<IConfusion> DefaultConfusions = new ObservableCollection<IConfusion>();
        public static readonly ObservableCollection<Packer> DefaultPackers = new ObservableCollection<Packer>();

        public Prj()
        {
            Confusions = new ObservableCollection<IConfusion>(DefaultConfusions);
            Confusions.CollectionChanged += (sender, e) => OnChildChanged();
            Packers = new ObservableCollection<Packer>(DefaultPackers);
            Packers.CollectionChanged += (sender, e) => OnChildChanged();
            Plugins = new ObservableCollection<string>();
            Plugins.CollectionChanged += (sender, e) => OnChildChanged();
            Assemblies = new ObservableCollection<PrjAssembly>();
            Assemblies.CollectionChanged += (sender, e) => OnChildChanged();
            Rules = new ObservableCollection<PrjRule>();
            Rules.CollectionChanged += (sender, e) => OnChildChanged();
        }

        public ObservableCollection<IConfusion> Confusions { get; private set; }
        public ObservableCollection<Packer> Packers { get; private set; }

        string snKey;
        public string StrongNameKey
        {
            get { return snKey; }
            set
            {
                if (snKey != value)
                {
                    snKey = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("StrongNameKey"));
                }
            }
        }
        string output;
        public string OutputPath
        {
            get { return output; }
            set
            {
                if (output != value)
                {
                    output = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("OutputPath"));
                }
            }
        }
        string seed;
        public string Seed
        {
            get { return seed; }
            set
            {
                if (seed != value)
                {
                    seed = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("Seed"));
                }
            }
        }
        bool dbg;
        public bool Debug
        {
            get { return dbg; }
            set
            {
                if (dbg != value)
                {
                    dbg = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("Debug"));
                }
            }
        }

        string file;
        public string FileName
        {
            get { return file; }
            set
            {
                if (file != value)
                {
                    file = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("FileName"));
                }
            }
        }
        bool modified;
        public bool IsModified
        {
            get { return modified; }
            set
            {
                if (modified != value)
                {
                    modified = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("IsModified"));
                }
            }
        }

        public void LoadAssembly(Assembly asm, bool interact)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Loaded type :");
            bool h = false;
            foreach (Type type in asm.GetTypes())
            {
                if (typeof(Core.IConfusion).IsAssignableFrom(type) && type != typeof(Core.IConfusion))
                {
                    Confusions.Add(Activator.CreateInstance(type) as Core.IConfusion);
                    sb.AppendLine(type.FullName);
                    h = true;
                }
                if (typeof(Core.Packer).IsAssignableFrom(type) && type != typeof(Core.Packer))
                {
                    Packers.Add(Activator.CreateInstance(type) as Core.Packer);
                    sb.AppendLine(type.FullName);
                    h = true;
                }
            }
            if (!h) sb.AppendLine("NONE!");
            else
            {
                Plugins.Add(asm.Location);
                Sort();
            }
            if (interact)
                MessageBox.Show(sb.ToString(), "Confuser", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        void Sort()
        {
            for (int i = 0; i < Confusions.Count; i++)
                for (int j = i; j < Confusions.Count; j++)
                    if (Comparer<string>.Default.Compare(Confusions[i].Name, Confusions[j].Name) > 0)
                    {
                        var tmp = Confusions[i];
                        Confusions[i] = Confusions[j];
                        Confusions[j] = tmp;
                    }
            for (int i = 0; i < Packers.Count; i++)
                for (int j = i; j < Packers.Count; j++)
                    if (Comparer<string>.Default.Compare(Packers[i].Name, Packers[j].Name) > 0)
                    {
                        var tmp = Packers[i];
                        Packers[i] = Packers[j];
                        Packers[j] = tmp;
                    }
        }

        PrjConfig<Packer> packer;
        public PrjConfig<Packer> Packer
        {
            get { return packer; }
            set
            {
                if (packer != value)
                {
                    packer = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("Packer"));
                }
            }
        }
        public ObservableCollection<PrjAssembly> Assemblies { get; private set; }
        public ObservableCollection<PrjRule> Rules { get; private set; }
        public ObservableCollection<string> Plugins { get; private set; }

        void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (e.PropertyName != "FileName" && e.PropertyName != "IsModified")
                OnChildChanged();
            if (PropertyChanged != null)
                PropertyChanged(this, e);
        }
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnChildChanged()
        {
            IsModified = true;
        }

        public INotifyChildrenChanged Parent { get { return null; } }

        internal string GetBasePath()
        {
            return FileName == null ? null : System.IO.Path.GetDirectoryName(FileName);
        }

        public ConfuserProject ToCrProj()
        {
            ConfuserProject ret = new ConfuserProject();
            ret.OutputPath = output;
            ret.SNKeyPath = snKey;
            ret.Seed = seed;
            ret.Debug = dbg;
            ret.BasePath = GetBasePath();

            if (packer != null)
                ret.Packer = packer.ToCrConfig();
            foreach (string i in Plugins)
                ret.Plugins.Add(i);

            foreach (var i in Assemblies)
                ret.Add(i.ToCrAssembly());
            foreach (var i in Rules)
                ret.Rules.Add(i.ToCrRule());
            return ret;
        }
        public void FromConfuserProject(ConfuserProject prj)
        {
            output = prj.OutputPath;
            snKey = prj.SNKeyPath;
            seed = prj.Seed;
            dbg = prj.Debug;
            foreach (var i in prj.Plugins)
                LoadAssembly(Assembly.LoadFrom(i), false);
            if (prj.Packer != null)
            {
                this.packer = new PrjConfig<Packer>(Packers.Single(_ => _.ID == prj.Packer.Id), this);
                foreach (var j in prj.Packer.AllKeys)
                    this.packer.Add(new PrjArgument(this) { Name = j, Value = prj.Packer[j] });
            }
            foreach (var i in prj)
            {
                PrjAssembly asm = new PrjAssembly(this);
                asm.FromCrAssembly(this, i);
                Assemblies.Add(asm);
            }
            foreach (var i in prj.Rules)
            {
                PrjRule rule = new PrjRule(this);
                rule.FromCrRule(this, i);
                Rules.Add(rule);
            }
        }
    }
}
