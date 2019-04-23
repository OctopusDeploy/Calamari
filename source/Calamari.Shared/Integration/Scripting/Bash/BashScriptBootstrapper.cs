using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Util;
using Octostache;

namespace Calamari.Integration.Scripting.Bash
{
    public class BashScriptBootstrapper
    {
        public const string WindowsNewLine = "\r\n";

        private static readonly string BootstrapScriptTemplate;
        static readonly string SensitiveVariablePassword = AesEncryption.RandomString(16);
        static readonly AesEncryption VariableEncryptor = new AesEncryption(SensitiveVariablePassword);
        static readonly ICalamariFileSystem CalamariFileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();

        static BashScriptBootstrapper()
        {
            BootstrapScriptTemplate = EmbeddedResource.ReadEmbeddedText(typeof(BashScriptBootstrapper).Namespace + ".Bootstrap.sh");
        }

        public static string FormatCommandArguments(string bootstrapFile)
        {
            var encryptionKey = ToHex(AesEncryption.GetEncryptionKey(SensitiveVariablePassword));
            var commandArguments = new StringBuilder();
            commandArguments.AppendFormat("\"{0}\" \"{1}\"", bootstrapFile, encryptionKey);
            return commandArguments.ToString();
        }
        
        public static string PrepareConfigurationFile(string workingDirectory, CalamariVariableDictionary variables)
        {
            var configurationFile = Path.Combine(workingDirectory, "Configure." + Guid.NewGuid().ToString().Substring(10) + ".sh");

            var builder = new StringBuilder(BootstrapScriptTemplate);
            builder.Replace("#### VariableDeclarations ####", string.Join(Environment.NewLine, GetVariableSwitchConditions(variables)));

            using (var file = new FileStream(configurationFile, FileMode.CreateNew, FileAccess.Write))
            using (var writer = new StreamWriter(file, Encoding.ASCII))
            {
                writer.Write(builder.Replace(WindowsNewLine, Environment.NewLine));
                writer.Flush();
            }

            File.SetAttributes(configurationFile, FileAttributes.Hidden);
            return configurationFile;
        }

        static IEnumerable<string> GetVariableSwitchConditions(CalamariVariableDictionary variables)
        {
            return variables.GetNames().Select(variable =>
            {
                var variableValue = variables.IsSensitive(variable)
                    ? DecryptValueCommand(variables.Get(variable))
                    : string.Format("decode_servicemessagevalue \"{0}\"", EncodeValue(variables.Get(variable)));

                return string.Format("    \"{1}\"){0}   {2}   ;;{0}", Environment.NewLine, EncodeValue(variable), variableValue);
            });
        }

        static string DecryptValueCommand(string value)
        {
            var encrypted = VariableEncryptor.Encrypt(value ?? "");
            byte[] iv;
            var rawEncrypted = AesEncryption.ExtractIV(encrypted, out iv);
            
            return string.Format("decrypt_variable \"{0}\" \"{1}\"", Convert.ToBase64String(rawEncrypted), ToHex(iv));
        }

        static string ToHex(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", "");
        }

        static string EncodeValue(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? ""));
        }

        public static string FindBashExecutable()
        {
            //TODO: Get this working on Non mono (windows path on cygwin)
            //return (CalamariEnvironment.IsRunningOnNix || CalamariEnvironment.IsRunningOnMac) ? "/bin/bash" : @"C:\cygwin64\bin\bash.exe";
            return "bash";
        }

        static void EnsureValidUnixFile(string scriptFilePath)
        {
            var text = File.ReadAllText(scriptFilePath);
            text = text.Replace(WindowsNewLine, Environment.NewLine);
            File.WriteAllText(scriptFilePath, text);
        }

        public static string PrepareBootstrapFile(Script script, string configurationFile, string workingDirectory, VariableDictionary variables)
        {            
            var bootstrapFile = Path.Combine(workingDirectory, "Bootstrap." + Guid.NewGuid().ToString().Substring(10) + "." + Path.GetFileName(script.File));
            var scriptModulePaths = PrepareScriptModules(variables, workingDirectory);

            using (var file = new FileStream(bootstrapFile, FileMode.CreateNew, FileAccess.Write))
            using (var writer = new StreamWriter(file, Encoding.ASCII))
            {
                writer.NewLine = Environment.NewLine;
                writer.WriteLine("#!/bin/bash");
                writer.WriteLine("source \"" + configurationFile.Replace("\\", "\\\\") + "\"");
                foreach(var scriptModulePath in scriptModulePaths)
                    writer.WriteLine("source \"" + scriptModulePath.Replace("\\", "\\\\") + "\"");
                writer.WriteLine("source \"" + script.File.Replace("\\", "\\\\") + "\" " + script.Parameters);
                writer.Flush();
            }

            File.SetAttributes(bootstrapFile, FileAttributes.Hidden);
            EnsureValidUnixFile(script.File);
            return bootstrapFile;
        }
        
        static IEnumerable<string> PrepareScriptModules(VariableDictionary variables, string workingDirectory)
        {
            foreach (var variableName in variables.GetNames().Where(SpecialVariables.IsLibraryScriptModule))
            {
                if (SpecialVariables.GetLibraryScriptModuleLangauge(variables, variableName) == ScriptSyntax.Bash) {
                    var libraryScriptModuleName = SpecialVariables.GetLibraryScriptModuleName(variableName);
                    var name = "Library_" + new string(libraryScriptModuleName.Where(char.IsLetterOrDigit).ToArray()) + "_" + DateTime.Now.Ticks;
                    var moduleFileName = $"{name}.sh";
                    var moduleFilePath = Path.Combine(workingDirectory, moduleFileName);
                    Log.VerboseFormat("Writing script module '{0}' as bash script {1}. This script will be automatically imported - functions will automatically be in scope.", libraryScriptModuleName, moduleFileName, name);
                    Encoding utf8WithoutBom = new UTF8Encoding(false);
                    CalamariFileSystem.OverwriteFile(moduleFilePath, variables.Get(variableName), utf8WithoutBom);
                    EnsureValidUnixFile(moduleFilePath);
                    yield return moduleFilePath;
                }
            }
        }
    }
}
