using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Confuser.Core;
using Confuser.Core.Project;
using System.Xml;

namespace Confuser.Console
{
    class Program
    {
        static int ParseCommandLine(string[] args, out ConfuserProject proj)
        {
            proj = new ConfuserProject();

            if (args.Length == 1 && !args[0].StartsWith("-"))   //shortcut for -project
            {
                if (!File.Exists(args[0]))
                {
                    WriteLineWithColor(ConsoleColor.Red, string.Format("Error: File '{0}' not exist!", args[0]));
                    return 2;
                }
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(args[0]);
                try
                {
                    proj.Load(xmlDoc);
                }
                catch (Exception e)
                {
                    WriteLineWithColor(ConsoleColor.Red, string.Format("Error: Invalid project format! Message : {0}", e.Message));
                    return 4;
                }
                return 0;
            }

            bool? state = null;
            for (int i = 0; i < args.Length; i++)
            {
                string action = args[i].ToLower();
                if (!action.StartsWith("-") || i + 1 >= args.Length)
                {
                    WriteLineWithColor(ConsoleColor.Red, string.Format("Error: Invalid argument {0}!", action));
                    return 3;
                }
                action = action.Substring(1).ToLower();
                switch (action)
                {
                    case "project":
                        {
                            if (state == true)
                            {
                                WriteLineWithColor(ConsoleColor.Red, string.Format("Error: Invalid combination!"));
                                return 3;
                            }
                            if (!File.Exists(args[i + 1]))
                            {
                                WriteLineWithColor(ConsoleColor.Red, string.Format("Error: File '{0}' not exist!", args[i + 1]));
                                return 2;
                            }
                            try
                            {
                                XmlDocument xmlDoc = new XmlDocument();
                                xmlDoc.Load(args[i + 1]);
                                proj.Load(xmlDoc);
                                proj.BasePath = Path.GetDirectoryName(args[i + 1]);
                            }
                            catch (Exception e)
                            {
                                WriteLineWithColor(ConsoleColor.Red, string.Format("Error: Invalid project format! Message : {0}", e.Message));
                                return 4;
                            }
                            state = false;
                            i += 1;
                        } break;
                    case "preset":
                        {
                            if (state == false)
                            {
                                WriteLineWithColor(ConsoleColor.Red, string.Format("Error: Invalid combination!"));
                                return 3;
                            }
                            try
                            {
                                Rule rule = new Rule();
                                rule.Preset = (Preset)Enum.Parse(typeof(Preset), args[i + 1], true);
                                rule.Pattern = ".*";
                                proj.Rules.Add(rule);
                                i += 1;
                                state = true;
                            }
                            catch
                            {
                                WriteLineWithColor(ConsoleColor.Red, string.Format("Error: Invalid preset '{0}'!", args[i + 1]));
                                return 3;
                            }
                        } break;
                    case "input":
                        {
                            if (state == false)
                            {
                                WriteLineWithColor(ConsoleColor.Red, string.Format("Error: Invalid combination!"));
                                return 3;
                            }
                            int parameterCounter = i + 1;

                            for (int j = i + 1; j < args.Length && !args[j].StartsWith("-"); j++)
                            {
                                parameterCounter = j;
                                string inputParameter = args[j];

                                int lastBackslashPosition = inputParameter.LastIndexOf('\\') + 1;
                                string filename = inputParameter.Substring(lastBackslashPosition, inputParameter.Length - lastBackslashPosition);
                                string path = inputParameter.Substring(0, lastBackslashPosition);

                                try
                                {
                                    string[] fileList = Directory.GetFiles(path, filename);
                                    if (fileList.Length == 0)
                                    {
                                        WriteLineWithColor(ConsoleColor.Red, string.Format("Error: No files matching '{0}' in directory '{1}'!", filename));
                                        return 2;
                                    }
                                    else if (fileList.Length == 1)
                                    {
                                        proj.Add(new ProjectAssembly()
                                        {
                                            Path = fileList[0],
                                            IsMain = j == i + 1 && filename.Contains('?') == false && filename.Contains('*') == false
                                        });
                                    }
                                    else
                                    {
                                        foreach (string expandedFilename in fileList)
                                        {
                                            proj.Add(new ProjectAssembly() { Path = expandedFilename, IsMain = false });
                                        }
                                    }
                                }
                                catch (DirectoryNotFoundException)
                                {
                                    WriteLineWithColor(ConsoleColor.Red, string.Format("Error: Directory '{0}' does not exist!", path));
                                    return 2;
                                }
                            }
                            state = true;
                            i = parameterCounter;
                        } break;
                    case "output":
                        {
                            if (state == false)
                            {
                                WriteLineWithColor(ConsoleColor.Red, string.Format("Error: Invalid combination!"));
                                return 3;
                            }
                            if (!Directory.Exists(args[i + 1]))
                            {
                                WriteLineWithColor(ConsoleColor.Red, string.Format("Error: Directory '{0}' not exist!", args[i + 1]));
                                return 2;
                            }
                            proj.OutputPath = args[i + 1];
                            state = true;
                            i += 1;
                        } break;
                    case "snkey":
                        {
                            if (!File.Exists(args[i + 1]))
                            {
                                WriteLineWithColor(ConsoleColor.Red, string.Format("Error: File '{0}' not exist!", args[i + 1]));
                                return 2;
                            }
                            proj.SNKeyPath = args[i + 1];
                            state = true;
                            i += 1;
                        } break;
                }
            }

            if (proj.Count == 0 || string.IsNullOrEmpty(proj.OutputPath))
            {
                WriteLineWithColor(ConsoleColor.Red, "Error: Missing required arguments!");
                return 4;
            }


            return 0;
        }

        static int Main(string[] args)
        {
            ConsoleColor color = System.Console.ForegroundColor;
            System.Console.ForegroundColor = ConsoleColor.White;

            WriteLine("Confuser Version v" + typeof(Core.Confuser).Assembly.GetName().Version);
            WriteLine();

#if DEBUG
            for (int i = 0; i < 3; i++)
            {
                System.Console.Write('.');
                System.Threading.Thread.Sleep(1000);
            }
            WriteLine();
#endif


            try
            {
                if (args.Length == 0 || args[0] == "-help")
                {
                    PrintUsage();
                    return 0;
                }

                ConfuserProject proj;
                int error = ParseCommandLine(args, out proj);
                if (error != 0)
                {
                    return error;
                }

                Core.Confuser cr = new Core.Confuser();
                ConfuserParameter param = new ConfuserParameter();
                param.Project = proj;
                ConsoleLogger.Initalize(param.Logger);
                WriteLine("Start working.");
                WriteLine(new string('*', 15));
                cr.Confuse(param);

                return ConsoleLogger.ReturnValue;
            }
            finally
            {
                System.Console.ForegroundColor = color;
            }
        }

        static void PrintUsage()
        {
            WriteLine("Usage:");
            WriteLine("Confuser.Console.exe [-project <configuration file> | -preset <preset> -snkey <strong name key> -output <output directory> -input <input files>]");
        }

        static void WriteLineWithColor(ConsoleColor color, string txt)
        {
            ConsoleColor clr = System.Console.ForegroundColor;
            System.Console.ForegroundColor = color;
            System.Console.WriteLine(txt);
            System.Console.ForegroundColor = clr;
        }
        static void WriteLine(string txt)
        {
            System.Console.WriteLine(txt);
        }
        static void WriteLine()
        {
            System.Console.WriteLine();
        }
    }
}
