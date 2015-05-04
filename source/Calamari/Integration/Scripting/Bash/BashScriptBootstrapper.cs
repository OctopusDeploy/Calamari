using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting.ScriptCS;
using Octostache;

namespace Calamari.Integration.Scripting.Bash
{
    public class BashScriptBootstrapper
    {
        private static readonly string BootstrapScriptTemplate;

        static BashScriptBootstrapper()
        {
            BootstrapScriptTemplate = EmbeddedResource.ReadEmbeddedText(typeof(BashScriptBootstrapper).Namespace + ".Bootstrap.sh");
        }


        public static string FormatCommandArguments(string bootstrapFile)
        {
            var commandArguments = new StringBuilder();
            commandArguments.AppendFormat("\"{0}\"", bootstrapFile);
            return commandArguments.ToString();
        }

        
        public static string PrepareConfigurationFile(string workingDirectory, VariableDictionary variables)
        {
            var configurationFile = Path.Combine(workingDirectory, "Configure." + Guid.NewGuid().ToString().Substring(10) + ".sh");

            var builder = new StringBuilder(BootstrapScriptTemplate);
            builder.Replace("#### VariableDeclarations ####", string.Join(Environment.NewLine, GetVariableSwitchConditions(variables)));

            using (var writer = new StreamWriter(configurationFile, false, Encoding.ASCII))
            {
                writer.Write(builder.ToString());
                writer.Flush();
            }

            File.SetAttributes(configurationFile, FileAttributes.Hidden);
            return configurationFile;
        }

        static IEnumerable<string> GetVariableSwitchConditions(VariableDictionary variables)
        {
            return variables.GetNames().Select(variable => string.Format("    \"{1}\"){0}    echo \"{2}\" | openssl enc -base64 -d{0}    ;;{0}", Environment.NewLine, EncodeValue(variable), EncodeValue(variables.Get(variable))));
        }


        static string EncodeValue(string value)
        {
            return value == null ? "null" : Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        }

        public static string FindBashExecutable()
        {
            //TODO: Get this working on Non mono (windows path on cygwin)
            //return (CalamariEnvironment.IsRunningOnNix) ? "/bin/bash" : @"C:\cygwin64\bin\bash.exe";
            return "bash";
        }

        public static string PrepareBootstrapFile(string scriptFilePath, string configurationFile, string workingDirectory)
        {
            var bootstrapFile = Path.Combine(workingDirectory, "Bootstrap." + Guid.NewGuid().ToString().Substring(10) + "." + Path.GetFileName(scriptFilePath));

            using (var writer = new StreamWriter(bootstrapFile, false, Encoding.ASCII))
            {
                writer.WriteLine("#!/bin/bash");
                writer.WriteLine("source \"" + configurationFile.Replace("\\", "\\\\") + "\"");
                writer.WriteLine("source \"" + scriptFilePath.Replace("\\", "\\\\") + "\"");
                writer.Flush();
            }

            File.SetAttributes(bootstrapFile, FileAttributes.Hidden);
            return bootstrapFile;
        }
    }
}
