using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Collections.Specialized;
using System.Xml.Schema;
using Mono.Cecil;

namespace Confuser.Core.Project
{
    public class ProjectAssembly
    {
        public string Path { get; set; }
        public bool IsMain { get; set; }

        public void Import(AssemblyDefinition assembly)
        {
            this.Path = assembly.MainModule.FullyQualifiedName;
        }
        public AssemblyDefinition Resolve(string basePath)
        {
            if (basePath == null)
                return AssemblyDefinition.ReadAssembly(Path,
                    new ReaderParameters(ReadingMode.Immediate));
            else
                return AssemblyDefinition.ReadAssembly(
                    System.IO.Path.GetFullPath(System.IO.Path.Combine(basePath, Path)),
                    new ReaderParameters(ReadingMode.Immediate));
        }

        public XmlElement Save(XmlDocument xmlDoc)
        {
            XmlElement elem = xmlDoc.CreateElement("assembly", ConfuserProject.Namespace);

            XmlAttribute nameAttr = xmlDoc.CreateAttribute("path");
            nameAttr.Value = Path;
            elem.Attributes.Append(nameAttr);

            if (IsMain != false)
            {
                XmlAttribute mainAttr = xmlDoc.CreateAttribute("isMain");
                mainAttr.Value = IsMain.ToString().ToLower();
                elem.Attributes.Append(mainAttr);
            }

            return elem;
        }
        public void Load(XmlElement elem)
        {
            this.Path = elem.Attributes["path"].Value;
            if (elem.Attributes["isMain"] != null)
                this.IsMain = bool.Parse(elem.Attributes["isMain"].Value);
        }

        public override string ToString()
        {
            return Path;
        }
    }

    public enum SettingItemAction
    {
        Add,
        Remove
    }
    public class SettingItem<T> : NameValueCollection
    {
        public string Id { get; set; }
        public SettingItemAction Action { get; set; }

        public XmlElement Save(XmlDocument xmlDoc)
        {
            XmlElement elem = xmlDoc.CreateElement(typeof(T) == typeof(Packer) ? "packer" : "confusion", ConfuserProject.Namespace);

            XmlAttribute idAttr = xmlDoc.CreateAttribute("id");
            idAttr.Value = Id;
            elem.Attributes.Append(idAttr);

            if (Action != SettingItemAction.Add)
            {
                XmlAttribute pAttr = xmlDoc.CreateAttribute("action");
                pAttr.Value = Action.ToString().ToLower();
                elem.Attributes.Append(pAttr);
            }

            foreach (var i in this.AllKeys)
            {
                XmlElement arg = xmlDoc.CreateElement("argument", ConfuserProject.Namespace);

                XmlAttribute nameAttr = xmlDoc.CreateAttribute("name");
                nameAttr.Value = i;
                arg.Attributes.Append(nameAttr);
                XmlAttribute valAttr = xmlDoc.CreateAttribute("value");
                valAttr.Value = base[i];
                arg.Attributes.Append(valAttr);

                elem.AppendChild(arg);
            }

            return elem;
        }

        public void Load(XmlElement elem)
        {
            this.Id = elem.Attributes["id"].Value;
            if (elem.Attributes["action"] != null)
                this.Action = (SettingItemAction)Enum.Parse(typeof(SettingItemAction), elem.Attributes["action"].Value, true);
            else
                this.Action = SettingItemAction.Add;
            foreach (XmlElement i in elem.ChildNodes.OfType<XmlElement>())
                this.Add(i.Attributes["name"].Value, i.Attributes["value"].Value);
        }
    }
    public class Rule : List<SettingItem<IConfusion>>
    {
        public string Pattern { get; set; }
        public Preset Preset { get; set; }
        public bool Inherit { get; set; }

        public XmlElement Save(XmlDocument xmlDoc)
        {
            XmlElement elem = xmlDoc.CreateElement("rule", ConfuserProject.Namespace);

            XmlAttribute ruleAttr = xmlDoc.CreateAttribute("pattern");
            ruleAttr.Value = Pattern;
            elem.Attributes.Append(ruleAttr);

            if (Preset != Preset.None)
            {
                XmlAttribute pAttr = xmlDoc.CreateAttribute("preset");
                pAttr.Value = Preset.ToString().ToLower();
                elem.Attributes.Append(pAttr);
            }

            if (Inherit != true)
            {
                XmlAttribute attr = xmlDoc.CreateAttribute("inherit");
                attr.Value = Inherit.ToString().ToLower();
                elem.Attributes.Append(attr);
            }

            foreach (var i in this)
                elem.AppendChild(i.Save(xmlDoc));

            return elem;
        }

        public void Load(XmlElement elem)
        {
            this.Pattern = elem.Attributes["pattern"].Value;

            if (elem.Attributes["preset"] != null)
                this.Preset = (Preset)Enum.Parse(typeof(Preset), elem.Attributes["preset"].Value, true);
            else
                this.Preset = Preset.None;

            if (elem.Attributes["inherit"] != null)
                this.Inherit = bool.Parse(elem.Attributes["inherit"].Value);
            else
                this.Inherit = true;

            foreach (XmlElement i in elem.ChildNodes.OfType<XmlElement>())
            {
                var x = new SettingItem<IConfusion>();
                x.Load(i);
                this.Add(x);
            }
        }

        public Rule Clone()
        {
            Rule ret = new Rule();
            ret.Preset = this.Preset;
            ret.Pattern = this.Pattern;
            ret.Inherit = this.Inherit;
            foreach (var i in this)
            {
                var item = new SettingItem<IConfusion>();
                item.Id = i.Id;
                item.Action = i.Action;
                foreach (var j in i.AllKeys)
                    item.Add(j, i[j]);
                ret.Add(item);
            }
            return ret;
        }
    }

    public class ProjectValidationException : Exception
    {
        internal ProjectValidationException(List<Tuple<string, XmlSchemaException>> exceptions)
            : base(exceptions[0].Item1)
        {
            Errors = exceptions;
        }
        public IList<Tuple<string, XmlSchemaException>> Errors { get; private set; }
    }

    public class ConfuserProject : List<ProjectAssembly>
    {
        public ConfuserProject()
        {
            Plugins = new List<string>();
            Rules = new List<Rule>();
        }
        public IList<string> Plugins { get; private set; }
        public IList<Rule> Rules { get; private set; }
        public string Seed { get; set; }
        public bool Debug { get; set; }
        public string OutputPath { get; set; }
        public string BasePath { get; set; }
        public string SNKeyPath { get; set; }   //For pfx, use "xxx.pfx|password"
        public SettingItem<Packer> Packer { get; set; }

        public static readonly XmlSchema Schema = XmlSchema.Read(typeof(ConfuserProject).Assembly.GetManifestResourceStream("Confuser.Core.ConfuserPrj.xsd"), null);
        public const string Namespace = "http://confuser.codeplex.com";
        public XmlDocument Save()
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Schemas.Add(Schema);

            XmlElement elem = xmlDoc.CreateElement("project", Namespace);

            XmlAttribute outputAttr = xmlDoc.CreateAttribute("outputDir");
            outputAttr.Value = OutputPath;
            elem.Attributes.Append(outputAttr);

            XmlAttribute snAttr = xmlDoc.CreateAttribute("snKey");
            snAttr.Value = SNKeyPath;
            elem.Attributes.Append(snAttr);

            if (Seed != null)
            {
                XmlAttribute seedAttr = xmlDoc.CreateAttribute("seed");
                seedAttr.Value = Seed;
                elem.Attributes.Append(seedAttr);
            }

            if (Debug != false)
            {
                XmlAttribute debugAttr = xmlDoc.CreateAttribute("debug");
                debugAttr.Value = Debug.ToString().ToLower();
                elem.Attributes.Append(debugAttr);
            }

            foreach (var i in Plugins)
            {
                XmlElement plug = xmlDoc.CreateElement("plugin", Namespace);

                XmlAttribute pathAttr = xmlDoc.CreateAttribute("path");
                pathAttr.Value = i;
                plug.Attributes.Append(pathAttr);

                elem.AppendChild(plug);
            }

            foreach (var i in Rules)
                elem.AppendChild(i.Save(xmlDoc));

            if (Packer != null)
                elem.AppendChild(Packer.Save(xmlDoc));

            foreach (var i in this)
                elem.AppendChild(i.Save(xmlDoc));

            xmlDoc.AppendChild(elem);
            return xmlDoc;
        }
        public void Load(XmlDocument doc)
        {
            doc.Schemas.Add(Schema);
            List<Tuple<string, XmlSchemaException>> exceptions = new List<Tuple<string, XmlSchemaException>>();
            doc.Validate((sender, e) =>
            {
                if (e.Severity != XmlSeverityType.Error) return;
                exceptions.Add(new Tuple<string, XmlSchemaException>(e.Message, e.Exception));
            });
            if (exceptions.Count > 0)
            {
                throw new ProjectValidationException(exceptions);
            }

            XmlElement docElem = doc.DocumentElement;

            this.OutputPath = docElem.Attributes["outputDir"].Value;
            this.SNKeyPath = docElem.Attributes["snKey"].Value;

            if (docElem.Attributes["seed"] != null)
                this.Seed = docElem.Attributes["seed"].Value;
            else
                this.Seed = null;

            if (docElem.Attributes["debug"] != null)
                this.Debug = bool.Parse(docElem.Attributes["debug"].Value);
            else
                this.Debug = false;

            foreach (XmlElement i in docElem.ChildNodes.OfType<XmlElement>())
            {
                if (i.Name == "plugin")
                {
                    Plugins.Add(i.Attributes["path"].Value);
                }
                else if (i.Name == "rule")
                {
                    Rule settings = new Rule();
                    settings.Load(i);
                    Rules.Add(settings);
                }
                else if (i.Name == "packer")
                {
                    Packer = new SettingItem<Packer>();
                    Packer.Load(i);
                }
                else
                {
                    ProjectAssembly asm = new ProjectAssembly();
                    asm.Load(i);
                    this.Add(asm);
                }
            }
        }
    }
}
