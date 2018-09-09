using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using Mono.Cecil;
using System.Collections.Specialized;
using System.IO;

namespace Confuser.Core
{
    public enum Priority
    {
        Safe = 1,
        CodeLevel = 2,
        FieldLevel = 3,
        MethodLevel = 4,
        TypeLevel = 5,
        AssemblyLevel = 6,
        MetadataLevel = 7,
        PELevel = 8
    }
    [Flags]
    public enum Target
    {
        Types = 1,
        Methods = 2,
        Fields = 4,
        Events = 8,
        Properties = 16,
        Module = 32,
        All = 63,
    }
    public enum Preset
    {
        None = 0,
        Minimum = 1,
        Normal = 2,
        Aggressive = 3,
        Maximum = 4,
    }
    [Flags]
    public enum Behaviour
    {
        Inject = 1,
        AlterCode = 2,
        Encrypt = 4,
        AlterStructure = 8,
    }
    [Flags]
    public enum HelperAttribute
    {
        NoInjection = 1,
        NoAlter = 2,
        NoEncrypt = 4,
    }

    public class ConfusionParameter
    {
        object target;
        NameValueCollection parameters = new NameValueCollection();
        NameValueCollection globalParams = new NameValueCollection();

        public object Target { get { return target; } set { target = value; } }
        public NameValueCollection Parameters { get { return parameters; } internal set { parameters = value; } }
        public NameValueCollection GlobalParameters { get { return globalParams; } internal set { globalParams = value; } }
    }

    public interface IConfusion
    {
        Phase[] Phases { get; }

        string ID { get; }
        string Name { get; }
        string Description { get; }

        Target Target { get; }
        Preset Preset { get; }
        bool StandardCompatible { get; }
        bool SupportLateAddition { get; }
        Behaviour Behaviour { get; }

        void Init();
        void Deinit();
    }

    public abstract class Phase
    {
        internal Phase() { }
        Confuser cr;
        internal Confuser Confuser { get { return cr; } set { cr = value; } }
        protected void Log(string message) { cr.Log(message); }
        protected void AddHelperAssembly(AssemblyDefinition asm)
        {
            ObfuscationSettings setting = new ObfuscationSettings((cr.settings.Cast<AssemblySetting?>().SingleOrDefault(_ => _.Value.IsMain) ?? cr.settings[0]).GlobalParameters);
            setting.Remove(Confusion);
            cr.param.Marker.MarkHelperAssembly(asm, setting, cr);
            foreach (Analyzer analyzer in cr.analyzers)
                analyzer.Analyze(new AssemblyDefinition[] { asm });
        }

        protected ConfuserParameter Parameter { get { return cr.param; } }
        protected void AddHelper(IMemberDefinition helper, HelperAttribute attr) { cr.helpers.Add(helper, attr); }
        protected IEnumerable<IMemberDefinition> GetHelpers() { return cr.helpers.Keys; }

        protected ObfuscationHelper ObfuscationHelper { get { return cr.ObfuscationHelper; } }
        protected Random Random { get { return cr.Random; } }
        protected ObfuscationDatabase Database { get { return cr.Database; } }

        public abstract IConfusion Confusion { get; }
        public abstract int PhaseID { get; }
        public abstract Priority Priority { get; }
        public abstract bool WholeRun { get; }
        public abstract void Initialize(ModuleDefinition mod);
        public abstract void DeInitialize();
        public virtual Analyzer GetAnalyzer() { return null; }
    }
    public abstract class StructurePhase : Phase
    {
        public abstract void Process(ConfusionParameter parameter);
    }
    public abstract class MetadataPhase : Phase
    {
        public abstract void Process(NameValueCollection parameters, MetadataProcessor.MetadataAccessor accessor);
        public override sealed void Initialize(ModuleDefinition mod) { }
        public override sealed void DeInitialize() { }
        public override sealed bool WholeRun { get { return true; } }
    }
    public abstract class PePhase : Phase
    {
        public abstract void Process(NameValueCollection parameters, Stream stream, MetadataProcessor.ImageAccessor accessor);
        public override sealed void Initialize(ModuleDefinition mod) { }
        public override sealed void DeInitialize() { }
        public override sealed bool WholeRun { get { return true; } }
    }
    public abstract class ImagePhase : Phase
    {
        public abstract void Process(NameValueCollection parameters, MetadataProcessor.ImageAccessor accessor);
        public override sealed void Initialize(ModuleDefinition mod) { }
        public override sealed void DeInitialize() { }
        public override sealed bool WholeRun { get { return true; } }
    }
}
