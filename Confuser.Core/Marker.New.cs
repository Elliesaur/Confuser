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
using Confuser.Core.Project;
using System.Collections;
using Mono.Cecil.Cil;
using Ref=System.Reflection;

namespace Confuser.Core
{
    public class ObfuscationSettings : Dictionary<IConfusion, NameValueCollection>
    {
        public ObfuscationSettings() { }
        public ObfuscationSettings(ObfuscationSettings settings)
        {
            foreach (var i in settings) Add(i.Key, new NameValueCollection(i.Value));
        }

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
            IsMain = false;
        }

        public AssemblyDefinition Assembly;
        public ObfuscationSettings GlobalParameters;
        public ModuleSetting[] Modules;
        public bool IsMain;
    }
    public struct ModuleSetting
    {
        public ModuleSetting(ModuleDefinition mod)
        {
            this.Module = mod;
            Parameters = null;
            Types = Mono.Empty<MemberSetting>.Array;
        }

        public ModuleDefinition Module;
        public ObfuscationSettings Parameters;
        public MemberSetting[] Types;
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
        public class Marking
        {
            public Marking()
            {
                inheritStack = new ObfuscationSettings[0x10];
                count = 0;
                CurrentConfusions = new ObfuscationSettings();
            }

            ObfuscationSettings[] inheritStack;
            int count;
            public ObfuscationSettings CurrentConfusions { get; private set; }

            struct LevelHolder : IDisposable
            {
                Marking m;
                public LevelHolder(Marking m)
                {
                    m.inheritStack[m.count] = m.CurrentConfusions;
                    m.count++;
                    if (m.count > 0)
                    {
                        int i = m.count - 1;
                        while (i > 0)
                        {
                            i--;
                        }
                        m.CurrentConfusions = new ObfuscationSettings(m.inheritStack[i]);
                    }
                    else
                        m.CurrentConfusions = new ObfuscationSettings();

                    this.m = m;
                }

                public void Dispose()
                {
                    m.count--;
                    m.inheritStack[m.count] = null;
                    if (m.count > 0)
                    {
                        m.CurrentConfusions = m.inheritStack[m.count - 1];
                    }
                }
            }

            public IDisposable Level()
            {
                return new LevelHolder(this);
            }
        }

        protected IDictionary<string, IConfusion> Confusions;
        protected IDictionary<string, Packer> Packers;
        public virtual void Initalize(IList<IConfusion> cions, IList<Packer> packs)
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
        static NameValueCollection Clone(NameValueCollection src)
        {
            NameValueCollection ret = new NameValueCollection();
            foreach (var i in src.AllKeys)
                ret.Add(i.ToLowerInvariant(), src[i]);
            return ret;
        }

        Confuser cr;
        protected Confuser Confuser { get { return cr; } set { cr = value; } }
        ConfuserProject proj;
        public virtual MarkerSetting MarkAssemblies(Confuser cr, Logger logger)
        {
            this.cr = cr;
            this.proj = cr.param.Project;
            MarkerSetting ret = new MarkerSetting();
            ret.Assemblies = new AssemblySetting[proj.Count];

            InitRules();

            Marking setting = new Marking();
            using (setting.Level())
            {
                for (int i = 0; i < proj.Count; i++)
                {
                    using (setting.Level())
                        ret.Assemblies[i] = _MarkAssembly(proj[i], setting);
                    logger._Progress(i + 1, proj.Count);
                }
                if (proj.Packer != null)
                {
                    ret.Packer = Packers[proj.Packer.Id];
                    ret.PackerParameters = Clone(proj.Packer);
                }
            }

            return ret;
        }

        internal void MarkHelperAssembly(AssemblyDefinition asm, ObfuscationSettings settings, Confuser cr)
        {
            AssemblySetting ret = new AssemblySetting(asm);
            ret.GlobalParameters = new ObfuscationSettings(settings);

            ret.Modules = asm.Modules.Select(_ => new ModuleSetting(_) { Parameters = new ObfuscationSettings() }).ToArray();
            foreach (var mod in asm.Modules)
                if (mod.GetType("<Module>").GetStaticConstructor() == null)
                {
                    MethodDefinition cctor = new MethodDefinition(".cctor", MethodAttributes.Private | MethodAttributes.HideBySig |
                        MethodAttributes.SpecialName | MethodAttributes.RTSpecialName |
                        MethodAttributes.Static, mod.TypeSystem.Void);
                    cctor.Body = new MethodBody(cctor);
                    cctor.Body.GetILProcessor().Emit(OpCodes.Ret);
                    mod.GetType("<Module>").Methods.Add(cctor);
                }

            cr.settings.Add(ret);
        }
        private AssemblySetting _MarkAssembly(ProjectAssembly asm, Marking mark)
        {
            AssemblySetting ret = MarkAssembly(asm, mark);
            ret.GlobalParameters = mark.CurrentConfusions;

            using (mark.Level())
            {
                List<ModuleSetting> modSettings = new List<ModuleSetting>();
                foreach (var m in ret.Assembly.Modules)
                    using (mark.Level())
                        MarkModule(ret, m, mark, modSettings);
                ret.Modules = modSettings.ToArray();
                return ret;
            }
        }
        protected virtual AssemblySetting MarkAssembly(ProjectAssembly asm, Marking mark)
        {
            AssemblySetting ret = new AssemblySetting(asm.Resolve(cr.param.Project.BasePath));
            ret.IsMain = asm.IsMain;
            ApplyRules(ret.Assembly, mark);
            return ret;
        }

        private void MarkModule(AssemblySetting parent, ModuleDefinition mod, Marking mark, List<ModuleSetting> settings)
        {
            ModuleSetting ret = MarkModule(parent, mod, mark);
            ret.Parameters = mark.CurrentConfusions;

            using (mark.Level())
            {
                List<MemberSetting> typeSettings = new List<MemberSetting>();
                foreach (var t in ret.Module.Types)
                    using (mark.Level())
                        MarkType(mod, t, mark, typeSettings);
                ret.Types = typeSettings.ToArray();
            }
            settings.Add(ret);
        }
        protected virtual ModuleSetting MarkModule(AssemblySetting parent, ModuleDefinition mod, Marking mark)
        {
            ModuleSetting ret = new ModuleSetting(mod);
            ApplyRules(mod, mark);
            return ret;
        }

        private void MarkType(ModuleDefinition mod, TypeDefinition type, Marking mark, List<MemberSetting> settings)
        {
            MemberSetting ret = MarkType(mod, type, mark);
            ret.Parameters = mark.CurrentConfusions;

            using (mark.Level())
            {
                List<MemberSetting> memSettings = new List<MemberSetting>();
                TypeDefinition typeDef = ret.Object as TypeDefinition;
                List<IMemberDefinition> mems = new List<IMemberDefinition>(typeDef.Methods.OfType<IMemberDefinition>().Concat(
                                  typeDef.Fields.OfType<IMemberDefinition>()).Concat(
                                  typeDef.Properties.OfType<IMemberDefinition>()).Concat(
                                  typeDef.Events.OfType<IMemberDefinition>()));
                foreach (var i in mems)
                    using (mark.Level())
                        MarkMember(ret, i, mark, memSettings);

                foreach (var i in typeDef.NestedTypes)
                    using (mark.Level())
                        MarkType(mod, i, mark, memSettings);

                ret.Members = memSettings.ToArray();
            }
            settings.Add(ret);
        }
        protected virtual MemberSetting MarkType(ModuleDefinition mod, TypeDefinition type, Marking mark)
        {
            MemberSetting ret = new MemberSetting(type);
            ApplyRules(type, mark);
            return ret;
        }

        private void MarkMember(MemberSetting parent, IMemberDefinition mem, Marking mark, List<MemberSetting> settings)
        {
            MemberSetting ret = MarkMember(parent, mem, mark);
            ret.Parameters = mark.CurrentConfusions;
            settings.Add(ret);
        }
        protected virtual MemberSetting MarkMember(MemberSetting parent, IMemberDefinition mem, Marking mark)
        {
            MemberSetting ret = new MemberSetting(mem);
            ApplyRules(mem, mark);
            return ret;
        }

        
        static IEnumerable<Ref.ObfuscationAttribute> GetObfuscationAttributes(object obj)
        {
            // resolve ApplyToMembers
            var memberDef = obj as IMemberDefinition;
            if (memberDef != null)
            {
                var parent = memberDef.DeclaringType;
                var parentAttributes = GetObfuscationAttributes(parent);
                if (parentAttributes != null)
                    foreach (var x in parentAttributes)
                        if (x.ApplyToMembers)
                            yield return x;
            }

            var attProv = obj as ICustomAttributeProvider;
            if (attProv != null)
            {
                foreach (var x in attProv.CustomAttributes)
                    if ( x.AttributeType.Name == "ObfuscationAttribute")
                        yield return new Ref.ObfuscationAttribute()
                             {
                                 ApplyToMembers = (from p in x.Properties where p.Name == "ApplyToMembers" select (bool)p.Argument.Value).FirstOrDefault(),
                                 Exclude = (from p in x.Properties where p.Name == "Exclude" select (bool)p.Argument.Value).FirstOrDefault(),
                                 Feature = (from p in x.Properties where p.Name == "Feature" select (string)p.Argument.Value).FirstOrDefault(),
                                 StripAfterObfuscation = (from p in x.Properties where p.Name == "StripAfterObfuscation" select (bool)p.Argument.Value).FirstOrDefault(),
                             };
            }
        }

        static string ToSignature(object obj)
        {
            if (obj is AssemblyDefinition)
                return (obj as AssemblyDefinition).Name.Name;
            else if (obj is ModuleDefinition)
                return (obj as ModuleDefinition).Name;
            else if (obj is TypeDefinition)
            {
                TypeDefinition typeDef = obj as TypeDefinition;
                if (typeDef.DeclaringType != null)
                    return string.Format("{0}.{1}",
                        ToSignature(typeDef.DeclaringType), typeDef.Name);
                else
                    return string.Format("{0}!{1}.{2}",
                        typeDef.Module.Assembly.Name.Name, typeDef.Namespace, typeDef.Name);
            }
            else if (obj is MethodDefinition)
            {
                MethodDefinition methodDef = obj as MethodDefinition;
                StringBuilder ret = new StringBuilder();
                ret.AppendFormat("{0}.{1}(",
                    ToSignature(methodDef.DeclaringType),
                    methodDef.Name);
                for (int i = 0; i < methodDef.Parameters.Count; i++)
                {
                    if (i != 0)
                        ret.AppendFormat(", {0}", methodDef.Parameters[i].ParameterType.Name);
                    else
                        ret.Append(methodDef.Parameters[i].ParameterType.Name);
                }
                if (methodDef.ReturnType.MetadataType == MetadataType.Void)
                    ret.Append(")");
                else
                    ret.AppendFormat(") : {0}", methodDef.ReturnType.Name);
                return ret.ToString();
            }
            else if (obj is FieldDefinition)
            {
                FieldDefinition fieldDef = obj as FieldDefinition;
                StringBuilder ret = new StringBuilder();
                ret.AppendFormat("{0}.{1} : {2}",
                    ToSignature(fieldDef.DeclaringType),
                    fieldDef.Name, fieldDef.FieldType.Name);
                return ret.ToString();
            }
            else if (obj is PropertyDefinition)
            {
                PropertyDefinition propertyDef = obj as PropertyDefinition;
                StringBuilder ret = new StringBuilder();
                ret.AppendFormat("{0}.{1}",
                    ToSignature(propertyDef.DeclaringType),
                    propertyDef.Name);
                if (propertyDef.Parameters.Count > 0)
                {
                    for (int i = 0; i < propertyDef.Parameters.Count; i++)
                    {
                        if (i != 0)
                            ret.AppendFormat(", {0}", propertyDef.Parameters[i].ParameterType.Name);
                        else
                            ret.AppendFormat("({0}", propertyDef.Parameters[i].ParameterType.Name);
                    }
                    ret.AppendFormat(") : {0}", propertyDef.PropertyType.Name);
                }
                else
                    ret.AppendFormat(" : {0}", propertyDef.PropertyType.Name);
                return ret.ToString();
            }
            else if (obj is EventDefinition)
            {
                EventDefinition eventDef = obj as EventDefinition;
                StringBuilder ret = new StringBuilder();
                ret.AppendFormat("{0}.{1} : {2}",
                    ToSignature(eventDef.DeclaringType),
                    eventDef.Name, eventDef.EventType.Name);
                return ret.ToString();
            }
            else
                throw new NotSupportedException();
        }

        Tuple<Regex, Rule>[] rules;
        void InitRules()
        {
            List<Tuple<Regex, Rule>> r = new List<Tuple<Regex, Rule>>();
            foreach (var i in proj.Rules)
            {
                try
                {
                    Regex regex = new Regex(i.Pattern);
                    r.Add(new Tuple<Regex, Rule>(regex, i));
                }
                catch (Exception ex)
                {
                    throw new ArgumentException("Invalid rule!", ex);
                }
            }
            rules = r.ToArray();
        }

        void ApplyRules(object obj, Marking mark)
        {
            string sig = ToSignature(obj);
            foreach (var i in rules)
            {
                if (!i.Item1.IsMatch(sig)) continue;

                if (!i.Item2.Inherit)
                    mark.CurrentConfusions.Clear();

                FillPreset(i.Item2.Preset, mark.CurrentConfusions);
                foreach (var j in i.Item2)
                {
                    if (j.Action == SettingItemAction.Add)
                        mark.CurrentConfusions[Confusions[j.Id]] = Clone(j);
                    else
                        mark.CurrentConfusions.Remove(Confusions[j.Id]);
                }
            }

            var obf = GetObfuscationAttributes(obj);
            if (obf != null)
                foreach (var i in obf)
                {
                    // Feature empty => all confusions
                    if (String.IsNullOrEmpty(i.Feature))
                    {
                        if (i.Exclude)
                            mark.CurrentConfusions.Clear();
                    }
                    else
                    {
                        if (!Confusions.ContainsKey(i.Feature))
                            continue;
                        var confusion = Confusions[i.Feature];
                        if (confusion != null)
                            if (!i.Exclude)
                                mark.CurrentConfusions[confusion] = new SettingItem<IConfusion>() { Id = i.Feature };
                            else
                                mark.CurrentConfusions.Remove(confusion);
                    }
                }
        }
    }
}
