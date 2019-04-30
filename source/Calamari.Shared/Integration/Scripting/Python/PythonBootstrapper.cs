using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting.Bash;
using Calamari.Util;
using Octostache;

namespace Calamari.Integration.Scripting.Python
{
    public class PythonBootstrapper
    {
        const string WindowsNewLine = "\r\n";

        static readonly string ConfigurationScriptTemplate;
        static readonly string InstallDependenciesScriptTemplate;
        static readonly string SensitiveVariablePassword = AesEncryption.RandomString(16);
        static readonly AesEncryption VariableEncryptor = new AesEncryption(SensitiveVariablePassword);
        static readonly ICalamariFileSystem CalamariFileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();

        static PythonBootstrapper()
        {
            ConfigurationScriptTemplate = EmbeddedResource.ReadEmbeddedText(typeof(PythonBootstrapper).Namespace + ".Configuration.py");
            InstallDependenciesScriptTemplate = EmbeddedResource.ReadEmbeddedText(typeof(PythonBootstrapper).Namespace + ".InstallDependencies.py");
        }

        public static string FormatCommandArguments(string bootstrapFile, string scriptParameters)
        {
            var encryptionKey = ToHex(AesEncryption.GetEncryptionKey(SensitiveVariablePassword));
            var commandArguments = new StringBuilder();
            commandArguments.Append($"\"{bootstrapFile}\" {scriptParameters} \"{encryptionKey}\"");
            return commandArguments.ToString();
        }
        
        public static string PrepareConfigurationFile(string workingDirectory, CalamariVariableDictionary variables)
        {
            var configurationFile = Path.Combine(workingDirectory, "Configure." + Guid.NewGuid().ToString().Substring(10) + ".py");

            var builder = new StringBuilder(ConfigurationScriptTemplate);
            builder.Replace("{{VariableDeclarations}}", $"octopusvariables = {{ {string.Join(",", GetVariables(variables))} }}");

            using (var file = new FileStream(configurationFile, FileMode.CreateNew, FileAccess.Write))
            using (var writer = new StreamWriter(file, Encoding.ASCII))
            {
                writer.Write(builder.Replace(WindowsNewLine, Environment.NewLine));
                writer.Flush();
            }

            File.SetAttributes(configurationFile, FileAttributes.Hidden);
            return configurationFile;
        }

        static IEnumerable<string> GetVariables(CalamariVariableDictionary variables)
        {
            return variables.GetNames().Select(variable =>
            {
                var variableValue = variables.IsSensitive(variable)
                    ? DecryptValueCommand(variables.Get(variable))
                    : $"decode(\"{EncodeValue(variables.Get(variable))}\")";

                return $"decode(\"{EncodeValue(variable)}\") : {variableValue}";
            });
        }

        static string DecryptValueCommand(string value)
        {
            var encrypted = VariableEncryptor.Encrypt(value ?? "");
            byte[] iv;
            var rawEncrypted = AesEncryption.ExtractIV(encrypted, out iv);
            
            return $"decrypt(\"{Convert.ToBase64String(rawEncrypted)}\",\"{ToHex(iv)}\")";
        }

        static string ToHex(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", "");
        }

        static string EncodeValue(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? ""));
        }

        public static string FindPythonExecutable()
        {
            return CalamariEnvironment.IsRunningOnWindows
                ? "python"
                : "python3";
        }

        static void EnsureValidUnixFile(string scriptFilePath)
        {
            var text = File.ReadAllText(scriptFilePath);
            text = text.Replace(WindowsNewLine, Environment.NewLine);
            File.WriteAllText(scriptFilePath, text);
        }

        public static (string bootstrapFile, string[] temporaryFiles) PrepareBootstrapFile(Script script, string workingDirectory, string configurationFile, VariableDictionary variables)
        {
            var bootstrapFile = Path.Combine(workingDirectory, "Bootstrap." + Guid.NewGuid().ToString().Substring(10) + "." + Path.GetFileName(script.File));
            var scriptModulePaths = PrepareScriptModules(variables, workingDirectory).ToArray();

            using (var file = new FileStream(bootstrapFile, FileMode.CreateNew, FileAccess.Write))
            using (var writer = new StreamWriter(file, Encoding.UTF8))
            {
                writer.WriteLine("from runpy import run_path");
                writer.WriteLine("configuration = run_path(\"" + configurationFile.Replace("\\", "\\\\") + "\")");
                writer.WriteLine("run_path(\"" + script.File.Replace("\\", "\\\\") + "\", configuration)");
                writer.Flush();
            }

            File.SetAttributes(bootstrapFile, FileAttributes.Hidden);
            EnsureValidUnixFile(script.File);
            return (bootstrapFile, scriptModulePaths);
        }
        
        static IEnumerable<string> PrepareScriptModules(VariableDictionary variables, string workingDirectory)
        {
            foreach (var variableName in variables.GetNames().Where(SpecialVariables.IsLibraryScriptModule))
            {
                if (SpecialVariables.GetLibraryScriptModuleLangauge(variables, variableName) == ScriptSyntax.Python) {
                    var libraryScriptModuleName = SpecialVariables.GetLibraryScriptModuleName(variableName);
                    var name = new string(libraryScriptModuleName.Where(x => char.IsLetterOrDigit(x) || x == '_').ToArray());
                    var moduleFileName = $"{name}.py";
                    Log.VerboseFormat("Writing script module '{0}' as python module {1}. Import this module via `import {2}`.", libraryScriptModuleName, moduleFileName, name);
                    var moduleFilePath = Path.Combine(workingDirectory, moduleFileName);
                    CalamariFileSystem.OverwriteFile(moduleFilePath, variables.Get(variableName), Encoding.UTF8);
                    yield return name;
                }
            }
        }

        public static string PrepareDependencyInstaller(string workingDirectory)
        {
            var dependencyInstallerFile = Path.Combine(workingDirectory, "InstallDependencies." + Guid.NewGuid().ToString().Substring(10) + ".py");

            var builder = new StringBuilder(InstallDependenciesScriptTemplate);

            using (var file = new FileStream(dependencyInstallerFile, FileMode.CreateNew, FileAccess.Write))
            using (var writer = new StreamWriter(file, Encoding.ASCII))
            {
                writer.Write(builder.Replace(WindowsNewLine, Environment.NewLine));
                writer.Flush();
            }

            File.SetAttributes(dependencyInstallerFile, FileAttributes.Hidden);
            return dependencyInstallerFile;
        }
    }
}