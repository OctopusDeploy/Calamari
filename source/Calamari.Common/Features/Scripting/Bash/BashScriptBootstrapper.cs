using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Calamari.Common.Variables;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Calamari.Util;

namespace Calamari.Common.Features.Scripting.Bash
{
    public class BashScriptBootstrapper
    {
        public const string WindowsNewLine = "\r\n";
        public const string LinuxNewLine = "\n";

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
        
        public static string PrepareConfigurationFile(string workingDirectory, IVariables variables)
        {
            var configurationFile = Path.Combine(workingDirectory, "Configure." + Guid.NewGuid().ToString().Substring(10) + ".sh");

            var builder = new StringBuilder(BootstrapScriptTemplate);
            builder.Replace("#### VariableDeclarations ####", string.Join(LinuxNewLine, GetVariableSwitchConditions(variables)));

            using (var file = new FileStream(configurationFile, FileMode.CreateNew, FileAccess.Write))
            using (var writer = new StreamWriter(file, Encoding.ASCII))
            {
                writer.Write(builder.Replace(WindowsNewLine, LinuxNewLine));
                writer.Flush();
            }

            File.SetAttributes(configurationFile, FileAttributes.Hidden);
            return configurationFile;
        }

        static IEnumerable<string> GetVariableSwitchConditions(IVariables variables)
        {
            return variables.GetNames().Select(variable =>
            {
                var variableValue = DecryptValueCommand(variables.Get(variable));
                return string.Format("    \"{1}\"){0}   {2}   ;;{0}", Environment.NewLine, EncodeValue(variable), variableValue);
            });
        }

        static string DecryptValueCommand(string value)
        {
            var encrypted = VariableEncryptor.Encrypt(value ?? "");
            var rawEncrypted = AesEncryption.ExtractIV(encrypted, out var iv);
            
            return $@"decrypt_variable ""{Convert.ToBase64String(rawEncrypted)}"" ""{ToHex(iv)}""";
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
            if (CalamariEnvironment.IsRunningOnWindows)
            {
                var systemFolder = Environment.GetFolderPath(Environment.SpecialFolder.System);
                return Path.Combine(systemFolder, "bash.exe");
            } 
            return "bash";
        }

        static void EnsureValidUnixFile(string scriptFilePath)
        {
            var text = File.ReadAllText(scriptFilePath);
            text = text.Replace(WindowsNewLine, LinuxNewLine);
            File.WriteAllText(scriptFilePath, text);
        }

        public static (string bootstrapFile, string[] temporaryFiles) PrepareBootstrapFile(Script script, string configurationFile, string workingDirectory, IVariables variables)
        {            
            var bootstrapFile = Path.Combine(workingDirectory, "Bootstrap." + Guid.NewGuid().ToString().Substring(10) + "." + Path.GetFileName(script.File));
            var scriptModulePaths = PrepareScriptModules(variables, workingDirectory).ToArray();

            using (var file = new FileStream(bootstrapFile, FileMode.CreateNew, FileAccess.Write))
            using (var writer = new StreamWriter(file, Encoding.ASCII))
            {
                writer.NewLine = LinuxNewLine;
                writer.WriteLine("#!/bin/bash");
                writer.WriteLine("source \"$(pwd)/" + Path.GetFileName(configurationFile) + "\"");
                writer.WriteLine("source \"$(pwd)/" + Path.GetFileName(script.File) + "\" " + script.Parameters);
                writer.Flush();
            }

            File.SetAttributes(bootstrapFile, FileAttributes.Hidden);
            EnsureValidUnixFile(script.File);
            return (bootstrapFile, scriptModulePaths);
        }
        
        static IEnumerable<string> PrepareScriptModules(IVariables variables, string workingDirectory)
        {
            foreach (var variableName in variables.GetNames().Where(ScriptVariables.IsLibraryScriptModule))
            {
                if (ScriptVariables.GetLibraryScriptModuleLanguage(variables, variableName) == ScriptSyntax.Bash) {
                    var libraryScriptModuleName = ScriptVariables.GetLibraryScriptModuleName(variableName);
                    var name = new string(libraryScriptModuleName.Where(char.IsLetterOrDigit).ToArray());
                    var moduleFileName = $"{name}.sh";
                    var moduleFilePath = Path.Combine(workingDirectory, moduleFileName);
                    Log.VerboseFormat("Writing script module '{0}' as bash script {1}. Import this via `source {1}`.", libraryScriptModuleName, moduleFileName, name);
                    Encoding utf8WithoutBom = new UTF8Encoding(false);
                    CalamariFileSystem.OverwriteFile(moduleFilePath, variables.Get(variableName), utf8WithoutBom);
                    EnsureValidUnixFile(moduleFilePath);
                    yield return moduleFilePath;
                }
            }
        }
    }
}
