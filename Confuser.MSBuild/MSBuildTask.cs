using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Build.Utilities;
using System.IO;
using System.Xml;
using Confuser.Core.Project;
using Microsoft.Build.Framework;
using Confuser.Core;

namespace Confuser
{
    public class MSBuildTask : Task
    {
        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.Low, "Confuser Version v{0}\n", typeof(Core.Confuser).Assembly.GetName().Version);


            string crproj = Path.Combine(
                Path.GetDirectoryName(BuildEngine.ProjectFileOfTaskNode),
                CrProj);

            if (!File.Exists(crproj))
            {
                Log.LogError("Confuser", "CR001", "Project", "",
                    0, 0, 0, 0,
                    string.Format("Error: Crproj file '{0}' not exist!", crproj));
                return false;
            }

            XmlDocument xmlDoc = new XmlDocument();
            ConfuserProject proj = new ConfuserProject();
            bool err = false;
            try
            {
                XmlReaderSettings settings = new XmlReaderSettings();
                settings.Schemas.Add(ConfuserProject.Schema);
                settings.ValidationType = ValidationType.Schema;
                settings.ValidationEventHandler += (sender, e) =>
                {
                    Log.LogError("Confuser", "CR002", "Project", crproj,
                        e.Exception.LineNumber, e.Exception.LinePosition,
                        e.Exception.LineNumber, e.Exception.LinePosition,
                        e.Message);
                    err = true;
                };
                var rdr = XmlReader.Create(crproj, settings);
                xmlDoc.Load(rdr);
                rdr.Close();
                if (err)
                    return false;
                proj.Load(xmlDoc);
            }
            catch (Exception ex)
            {
                Log.LogError("Confuser", "CR002", "Project", crproj,
                    0, 0, 0, 0,
                    ex.Message);
                return false;
            }
            proj.BasePath = BasePath;

            Core.Confuser cr = new Core.Confuser();
            ConfuserParameter param = new ConfuserParameter();
            param.Project = proj;

            var logger = new MSBuildLogger(Log);
            logger.Initalize(param.Logger);

            Log.LogMessage(MessageImportance.Low, "Start working.");
            Log.LogMessage(MessageImportance.Low, "***************");
            cr.Confuse(param);

            return logger.ReturnValue;
        }

        public string CrProj { get; set; }
        public string BasePath { get; set; }
    }
}
