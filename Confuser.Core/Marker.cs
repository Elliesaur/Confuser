using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Xml;

namespace Confuser.Core
{
    public class ObfuscationSettings : Dictionary<IConfusion, NameValueCollection>
    {
        public ObfuscationSettings() { }
        public ObfuscationSettings(ObfuscationSettings settings) : base(settings) { }

        public bool IsEmpty() { return this.Count == 0; }
    }
    public struct MarkerSetting
    {
        public Packer Packer;
        public NameValueCollection PackerParameters;
        public AssemblySetting[] Assemblies;
    }
    public struct AssemblySetting
    {
        public AssemblySetting(AssemblyDefinition asm)
        {
            this.Assembly = asm;
            GlobalParameters = null;
            Modules = Mono.Empty<ModuleSetting>.Array;
        }

        public AssemblyDefinition Assembly;
        public ObfuscationSettings GlobalParameters;
        public ModuleSetting[] Modules;
    }
    public struct ModuleSetting
    {
        public ModuleSetting(ModuleDefinition mod)
        {
            this.Module = mod;
            Parameters = null;
            Members = Mono.Empty<MemberSetting>.Array;
        }

        public ModuleDefinition Module;
        public ObfuscationSettings Parameters;
        public MemberSetting[] Members;
    }
    public struct MemberSetting
    {
        public MemberSetting(IMemberDefinition obj)
        {
            this.Object = obj;
            Parameters = null;
            Members = Mono.Empty<MemberSetting>.Array;
        }
        public IMemberDefinition Object;
        public ObfuscationSettings Parameters;
        public MemberSetting[] Members;
    }

    public class Marker
    {
        //public static readonly List<string> FrameworkAssemblies;
        static Marker()
        {
            //FrameworkAssemblies = new List<string>();
            //foreach (FileInfo file in Directory.GetParent(Environment.GetFolderPath(Environment.SpecialFolder.System)).GetDirectories("Microsoft.NET")[0].GetFiles("FrameworkList.xml", SearchOption.AllDirectories))
            //{
            //    XmlDocument doc = new XmlDocument();
            //    doc.Load(file.FullName);
            //    foreach (XmlNode xn in doc.SelectNodes("/FileList/File"))
            //    {
            //        byte[] tkn = new byte[8];
            //        string tknStr = xn.Attributes["PublicKeyToken"].Value;
            //        for (int i = 0; i < 8; i++)
            //            tkn[i] = Convert.ToByte(tknStr.Substring(i * 2, 2), 16);
            //        FrameworkAssemblies.Add(string.Format("{0}/{1}", xn.Attributes["AssemblyName"].Value, BitConverter.ToString(tkn ?? new byte[0])));
            //    }
            //}
            //foreach (string file in Directory.GetFiles(Directory.GetDirectories(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Reference Assemblies")[0], "FrameworkList.xml", SearchOption.AllDirectories))
            //{
            //    XmlDocument doc = new XmlDocument();
            //    doc.Load(file);
            //    foreach (XmlNode xn in doc.SelectNodes("/FileList/File"))
            //    {
            //        byte[] tkn = new byte[8];
            //        string tknStr = xn.Attributes["PublicKeyToken"].Value;
            //        for (int i = 0; i < 8; i++)
            //            tkn[i] = Convert.ToByte(tknStr.Substring(i * 2, 2), 16);
            //        FrameworkAssemblies.Add(string.Format("{0}/{1}", xn.Attributes["AssemblyName"].Value, BitConverter.ToString(tkn ?? new byte[0])));
            //    }
            //}
        }

        public class Marking
        {
            public Marking()
            {
                inheritStack = new Stack<ObfuscationSettings>();
                StartLevel();
            }

            Stack<ObfuscationSettings> inheritStack;
            public ObfuscationSettings CurrentConfusions { get; private set; }

            public void StartLevel()
            {
                if (inheritStack.Count > 0)
                    CurrentConfusions = new ObfuscationSettings(inheritStack.Peek());
                else
                    CurrentConfusions = new ObfuscationSettings();
                inheritStack.Push(CurrentConfusions);
            }
            public void LeaveLevel()
            {
                inheritStack.Pop();
                CurrentConfusions = inheritStack.Peek();
            }
            public void SkipLevel()
            {
                if (inheritStack.Count > 1)
                    CurrentConfusions = new ObfuscationSettings(inheritStack.ToArray()[inheritStack.Count - 2]);
                else
                    CurrentConfusions = new ObfuscationSettings();
                inheritStack.Push(CurrentConfusions);
            }
        }

        protected IDictionary<string, IConfusion> Confusions;
        protected IDictionary<string, Packer> Packers;
        public virtual void Initalize(IConfusion[] cions, Packer[] packs)
        {
            Confusions = new Dictionary<string, IConfusion>();
            foreach (IConfusion c in cions)
                Confusions.Add(c.ID, c);
            Packers = new Dictionary<string, Packer>();
            foreach (Packer pack in packs)
                Packers.Add(pack.ID, pack);
        }
        private void FillPreset(Preset preset, ObfuscationSettings cs)
        {
            foreach (IConfusion i in Confusions.Values)
                if (i.Preset <= preset && !cs.ContainsKey(i))
                    cs.Add(i, new NameValueCollection());
        }

        Confuser cr;
        protected Confuser Confuser { get { return cr; } set { cr = value; } }
        private bool ProcessAttribute(ICustomAttributeProvider provider, Marking setting)
        {
            CustomAttribute att = GetAttribute(provider.CustomAttributes, "ConfusingAttribute");
            if (att == null)
            {
                setting.StartLevel();
                return false;
            }

            CustomAttributeNamedArgument stripArg = att.Properties.FirstOrDefault(arg => arg.Name == "StripAfterObfuscation");
            bool strip = true;
            if (!stripArg.Equals(default(CustomAttributeNamedArgument)))
                strip = (bool)stripArg.Argument.Value;

            if (strip)
                provider.CustomAttributes.Remove(att);

            CustomAttributeNamedArgument excludeArg = att.Properties.FirstOrDefault(arg => arg.Name == "Exclude");
            bool exclude = false;
            if (!excludeArg.Equals(default(CustomAttributeNamedArgument)))
                exclude = (bool)excludeArg.Argument.Value;

            if (exclude)
                setting.CurrentConfusions.Clear();

            CustomAttributeNamedArgument applyToMembersArg = att.Properties.FirstOrDefault(arg => arg.Name == "ApplyToMembers");
            bool applyToMembers = true;
            if (!applyToMembersArg.Equals(default(CustomAttributeNamedArgument)))
                applyToMembers = (bool)applyToMembersArg.Argument.Value;

            if (applyToMembers)
                setting.StartLevel();
            else
                setting.SkipLevel();
            try
            {
                if (!exclude)
                {
                    CustomAttributeNamedArgument featureArg = att.Properties.FirstOrDefault(arg => arg.Name == "Config");
                    string feature = "all";
                    if (!featureArg.Equals(default(CustomAttributeNamedArgument)))
                        feature = (string)featureArg.Argument.Value;

                    if (string.Equals(feature, "all", StringComparison.OrdinalIgnoreCase))
                        FillPreset(Preset.Maximum, setting.CurrentConfusions);
                    else if (string.Equals(feature, "default", StringComparison.OrdinalIgnoreCase))
                        FillPreset(Preset.Normal, setting.CurrentConfusions);
                    else
                        ProcessConfig(feature, setting.CurrentConfusions);
                }

                return exclude && applyToMembers;
            }
            catch
            {
                cr.Log("Warning: Cannot process ConfusingAttribute at '" + provider.ToString() + "'. ConfusingAttribute ignored.");
                return false;
            }
        }
        private CustomAttribute GetAttribute(Collection<CustomAttribute> attributes, string name)
        {
            return attributes.FirstOrDefault((att) => att.AttributeType.FullName == name);
        }
        private void ProcessConfig(string cfg, ObfuscationSettings cs)
        {
            MatchCollection matches = Regex.Matches(cfg, @"(\+|\-|)\[([^,\]]*)(?:,([^\]]*))?\]");
            foreach (Match match in matches)
            {
                string id = match.Groups[2].Value.ToLower();
                switch (match.Groups[1].Value)
                {
                    case null:
                    case "":
                    case "+":
                        if (id == "preset")
                        {
                            FillPreset((Preset)Enum.Parse(typeof(Preset), match.Groups[3].Value, true), cs);
                        }
                        else if (id == "new")
                        {
                            cs.Clear();
                        }
                        else
                        {
                            if (!Confusions.ContainsKey(id))
                            {
                                cr.Log("Warning: Cannot find confusion id '" + id + "'.");
                                break;
                            }
                            IConfusion now = (from i in cs.Keys where i.ID == id select i).FirstOrDefault() ?? Confusions[id];
                            if (!cs.ContainsKey(now)) cs[now] = new NameValueCollection();
                            NameValueCollection nv = cs[now];
                            if (!string.IsNullOrEmpty(match.Groups[3].Value))
                            {
                                foreach (string param in match.Groups[3].Value.Split(','))
                                {
                                    string[] p = param.Split('=');
                                    if (p.Length == 1)
                                        nv[p[0].ToLower()] = "true";
                                    else
                                        nv[p[0].ToLower()] = p[1];
                                }
                            }
                        }
                        break;
                    case "-":
                        cs.Remove((from i in cs.Keys where i.ID == id select i).FirstOrDefault());
                        break;
                }
            }
        }
        private void ProcessPackers(ICustomAttributeProvider provider, out NameValueCollection param, out Packer packer)
        {
            CustomAttribute attr = GetAttribute(provider.CustomAttributes, "PackerAttribute");

            if (attr == null) { param = null; packer = null; return; }
            CustomAttributeNamedArgument stripArg = attr.Properties.FirstOrDefault(arg => arg.Name == "StripAfterObfuscation");
            bool strip = true;
            if (!stripArg.Equals(default(CustomAttributeNamedArgument)))
                strip = (bool)stripArg.Argument.Value;

            if (strip)
                provider.CustomAttributes.Remove(attr);

            CustomAttributeNamedArgument cfgArg = attr.Properties.FirstOrDefault(arg => arg.Name == "Config");
            string cfg = "";
            if (!cfgArg.Equals(default(CustomAttributeNamedArgument)))
                cfg = (string)cfgArg.Argument.Value;
            if (string.IsNullOrEmpty(cfg)) { param = null; packer = null; return; }

            param = new NameValueCollection();

            Match match = Regex.Match(cfg, @"([^:]+)(?::(?:([^=]*=[^,]*),?)*)?");
            packer = Packers[match.Groups[1].Value];
            foreach (Capture arg in match.Groups[2].Captures)
            {
                string[] args = arg.Value.Split('=');
                param.Add(args[0], args[1]);
            }
        }

        public virtual MarkerSetting MarkAssemblies(IList<AssemblyDefinition> asms, Preset preset, Confuser cr, EventHandler<LogEventArgs> err)
        {
            this.cr = cr;
            MarkerSetting ret = new MarkerSetting();
            ret.Assemblies = new AssemblySetting[asms.Count];

            for (int i = 0; i < asms.Count; i++)
            {
                Marking setting = new Marking();
                FillPreset(preset, setting.CurrentConfusions);
                ret.Assemblies[i] = MarkAssembly(asms[i], setting);
                if (i == 0)
                {
                    NameValueCollection param;
                    Packer packer;
                    ProcessPackers(asms[i], out param, out packer);
                    ret.Packer = packer;
                    ret.PackerParameters = param;
                }
            }

            return ret;
        }

        internal void MarkHelperAssembly(AssemblyDefinition asm, Confuser cr)
        {
            cr.settings.Add(MarkAssembly(asm, new Marking()));
        }
        private AssemblySetting MarkAssembly(AssemblyDefinition asm, Marking mark)
        {
            bool exclude;
            AssemblySetting ret = MarkAssembly(asm, mark, out exclude);

            ret.GlobalParameters = mark.CurrentConfusions;

            if (!exclude)
            {
                List<ModuleSetting> modSettings = new List<ModuleSetting>();
                foreach (ModuleDefinition mod in asm.Modules)
                    MarkModule(mod, mark, modSettings);
                ret.Modules = modSettings.ToArray();
            }

            mark.LeaveLevel();
            return ret;
        }
        protected virtual AssemblySetting MarkAssembly(AssemblyDefinition asm, Marking mark, out bool exclude)
        {
            exclude = ProcessAttribute(asm, mark);
            return new AssemblySetting(asm);
        }

        private void MarkModule(ModuleDefinition mod, Marking mark, List<ModuleSetting> settings)
        {
            bool exclude;
            ModuleSetting ret = MarkModule(mod, mark, out exclude);

            ret.Parameters = mark.CurrentConfusions;

            if (!exclude)
            {
                List<MemberSetting> typeSettings = new List<MemberSetting>();
                foreach (TypeDefinition type in mod.Types)
                {
                    mark.StartLevel();
                    MarkType(type, mark, typeSettings);
                    mark.LeaveLevel();
                }
                ret.Members = typeSettings.ToArray();
            }

            settings.Add(ret);

            mark.LeaveLevel();
        }
        protected virtual ModuleSetting MarkModule(ModuleDefinition mod, Marking mark, out bool exclude)
        {
            exclude = ProcessAttribute(mod, mark);
            return new ModuleSetting(mod);
        }

        private void MarkType(TypeDefinition type, Marking mark, List<MemberSetting> settings)
        {
            bool exclude;
            MemberSetting ret = MarkType(type, mark, out exclude);

            ret.Parameters = mark.CurrentConfusions;

            if (!exclude)
            {
                List<MemberSetting> memSettings = new List<MemberSetting>();

                foreach (TypeDefinition nType in type.NestedTypes)
                {
                    mark.StartLevel();
                    MarkType(nType, mark, memSettings);
                    mark.LeaveLevel();
                }

                foreach (MethodDefinition mtd in type.Methods)
                {
                    mark.StartLevel();
                    MarkMember(mtd, mark, Target.Methods, memSettings);
                    mark.LeaveLevel();
                }

                foreach (FieldDefinition fld in type.Fields)
                {
                    mark.StartLevel();
                    MarkMember(fld, mark, Target.Fields, memSettings);
                    mark.LeaveLevel();
                }

                foreach (PropertyDefinition prop in type.Properties)
                {
                    mark.StartLevel();
                    MarkMember(prop, mark, Target.Properties, memSettings);
                    mark.LeaveLevel();
                }

                foreach (EventDefinition evt in type.Events)
                {
                    mark.StartLevel();
                    MarkMember(evt, mark, Target.Events, memSettings);
                    mark.LeaveLevel();
                }

                ret.Members = memSettings.ToArray();
            }

            if (!ret.Parameters.IsEmpty() || ret.Members.Length != 0)
                settings.Add(ret);

            mark.LeaveLevel();
        }
        protected virtual MemberSetting MarkType(TypeDefinition type, Marking mark, out bool exclude)
        {
            exclude = ProcessAttribute(type, mark);
            return new MemberSetting(type);
        }

        private void MarkMember(IMemberDefinition mem, Marking mark, Target target, List<MemberSetting> settings)
        {
            if (target == Target.Methods && (mem as MethodDefinition).SemanticsAttributes != MethodSemanticsAttributes.None)
            {
                return;
            }

            bool exclude;
            MemberSetting ret = MarkMember(mem, mark, out exclude);

            ret.Parameters = mark.CurrentConfusions;

            if (!exclude)
            {
                List<MemberSetting> semSettings = new List<MemberSetting>();
                if (target == Target.Properties)
                {
                    PropertyDefinition prop = mem as PropertyDefinition;
                    List<MethodDefinition> sems = new List<MethodDefinition>();
                    if (prop.GetMethod != null)
                        sems.Add(prop.GetMethod);
                    if (prop.SetMethod != null)
                        sems.Add(prop.SetMethod);
                    if (prop.HasOtherMethods)
                        sems.AddRange(prop.OtherMethods);
                    foreach (MethodDefinition mtd in sems)
                    {
                        mark.StartLevel();
                        ProcessAttribute(mtd, mark);
                        semSettings.Add(new MemberSetting(mtd) { Parameters = mark.CurrentConfusions, Members = new MemberSetting[0] });
                        mark.LeaveLevel();
                        mark.LeaveLevel();
                    }
                }
                else if (target == Target.Events)
                {
                    EventDefinition evt = mem as EventDefinition;
                    List<MethodDefinition> sems = new List<MethodDefinition>();
                    if (evt.AddMethod != null)
                        sems.Add(evt.AddMethod);
                    if (evt.RemoveMethod != null)
                        sems.Add(evt.RemoveMethod);
                    if (evt.InvokeMethod != null)
                        sems.Add(evt.InvokeMethod);
                    if (evt.HasOtherMethods)
                        sems.AddRange(evt.OtherMethods);
                    foreach (MethodDefinition mtd in sems)
                    {
                        mark.StartLevel();
                        ProcessAttribute(mtd, mark);
                        semSettings.Add(new MemberSetting(mtd) { Parameters = mark.CurrentConfusions, Members = new MemberSetting[0] });
                        mark.LeaveLevel();
                        mark.LeaveLevel();
                    }
                }
                ret.Members = semSettings.ToArray();
            }

            if (!ret.Parameters.IsEmpty() || ret.Members.Length != 0)
                settings.Add(ret);

            mark.LeaveLevel();
        }
        protected virtual MemberSetting MarkMember(IMemberDefinition mem, Marking mark, out bool exclude)
        {
            exclude = ProcessAttribute(mem, mark);
            return new MemberSetting(mem);
        }

    }
    class CopyMarker : Marker
    {
        AssemblySetting origin;
        IConfusion exclude;
        public CopyMarker(AssemblySetting settings, IConfusion exclude) { origin = settings; this.exclude = exclude; }
        protected override AssemblySetting MarkAssembly(AssemblyDefinition asm, Marking mark, out bool exclude)
        {
            var ret = base.MarkAssembly(asm, mark, out exclude);
            mark.CurrentConfusions.Clear();
            foreach (var i in origin.GlobalParameters)
                if (i.Key != this.exclude)
                    mark.CurrentConfusions.Add(i.Key, i.Value);
            return ret;
        }
    }
}
