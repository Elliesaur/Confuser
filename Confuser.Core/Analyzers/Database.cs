using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Confuser.Core.Analyzers
{
    struct ReflectionMethod
    {
        public string typeName;
        public string mtdName;
        public int[] paramLoc;
        public string[] paramType;
    }

    static class Database
    {
        static Database()
        {
            Reflections = new Dictionary<string, ReflectionMethod>();
            string type = null;
            using (StringReader rdr = new StringReader(db))
            {
                while (true)
                {
                    string line = rdr.ReadLine();
                    if (line == "=") break;
                    if (type != null)
                    {
                        if (line == "")
                        {
                            type = null; continue;
                        }
                        ReflectionMethod mtd = new ReflectionMethod();
                        mtd.typeName = type;
                        mtd.mtdName = line.Substring(0, line.IndexOf('['));
                        string param = line.Substring(line.IndexOf('[') + 1, line.IndexOf(']') - line.IndexOf('[') - 1);
                        string[] pars = param.Split(',');
                        mtd.paramLoc = new int[pars.Length];
                        mtd.paramType = new string[pars.Length];
                        for (int i = 0; i < pars.Length; i++)
                        {
                            mtd.paramLoc[i] = int.Parse(pars[i].Split(':')[0]);
                            mtd.paramType[i] = pars[i].Split(':')[1];
                        }
                        Reflections.Add(mtd.typeName + "::" + mtd.mtdName, mtd);
                    }
                    else
                    {
                        type = line;
                    }
                }
            }

            ExcludeAttributes = new List<string>();
            using (StringReader rdr = new StringReader(exclude))
            {
                while (true)
                {
                    string line = rdr.ReadLine();
                    if (line == "=") break;
                    ExcludeAttributes.Add(line);
                }
            }
        }

        public static readonly Dictionary<string, ReflectionMethod> Reflections;
        public static readonly List<string> ExcludeAttributes;
        const string db =
@"Microsoft.VisualBasic.CompilerServices.LateBinding
LateCall[0:This,1:Type,2:Target]
LateGet[0:This,1:Type,2:Target]
LateSet[0:This,1:Type,2:Target]
LateSetComplex[0:This,1:Type,2:Target]

Microsoft.VisualBasic.CompilerServices.NewLateBinding
LateCall[0:This,1:Type,2:Target]
LateCanEvaluate[0:This,1:Type,2:Target]
LateGet[0:This,1:Type,2:Target]
LateSet[0:This,1:Type,2:Target]
LateSetComplex[0:This,1:Type,2:Target]

System.Type
GetEvent[0:Type,1:Target]
GetField[0:Type,1:Target]
GetMember[0:Type,1:Target]
GetMethod[0:Type,1:Target]
GetNestedType[0:Type,1:Target]
GetProperty[0:Type,1:Target]
GetType[0:TargetType]
InvokeMember[0:Type,1:Target]
ReflectionOnlyGetType[0:TargetType]

System.Delegate
CreateDelegate[1:Type,2:Target]

System.Reflection.Assembly
GetType[1:TargetType]

System.Reflection.Module
GetType[1:TargetType]

System.Activator
CreateInstance[1:TargetType]
CreateInstanceFrom[1:TargetType]

System.AppDomain
CreateInstance[2:TargetType]
CreateInstanceFrom[2:TargetType]

System.Resources.ResourceManager
.ctor[0:TargetResource]

System.Configuration.SettingsBase
get_Item[0:Type,1:Target]
set_Item[0:Type,1:Target]

System.Windows.DependencyProperty
Register[0:Target,2:Type]
RegisterAttached[0:Target,2:Type]
RegisterAttachedReadOnly[0:Target,2:Type]
RegisterReadOnly[0:Target,2:Type]
=";

        const string exclude =
@"System.ServiceModel.ServiceContractAttribute
System.ServiceModel.OperationContractAttribute
System.Data.Services.Common.DataServiceKeyAttribute
System.Data.Services.Common.EntitySetAttribute
Microsoft.SqlServer.Server.SqlFacetAttribute
Microsoft.SqlServer.Server.SqlFunctionAttribute
Microsoft.SqlServer.Server.SqlMethodAttribute
Microsoft.SqlServer.Server.SqlProcedureAttribute
Microsoft.SqlServer.Server.SqlTriggerAttribute
Microsoft.SqlServer.Server.SqlUserDefinedAggregateAttribute
Microsoft.SqlServer.Server.SqlUserDefinedTypeAttribute
=";
    }
}
