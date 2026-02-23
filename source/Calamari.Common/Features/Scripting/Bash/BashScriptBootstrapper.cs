using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripts;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Features.Scripting.Bash
{
    public class BashScriptBootstrapper
    {
        public const string WindowsNewLine = "\r\n";
        public const string LinuxNewLine = "\n";

        static readonly string BootstrapScriptTemplate;
        static readonly string SensitiveVariablePassword = AesEncryption.RandomString(16);
        static readonly AesEncryption VariableEncryptor = AesEncryption.ForScripts(SensitiveVariablePassword);
        static readonly ICalamariFileSystem CalamariFileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();

        static BashScriptBootstrapper()
        {
            BootstrapScriptTemplate = EmbeddedResource.ReadEmbeddedText(typeof(BashScriptBootstrapper).Namespace + ".Bootstrap.sh");
        }

        public static string FormatCommandArguments(string bootstrapFile)
        {
            var encryptionKey = ToHex(VariableEncryptor.EncryptionKey);
            var commandArguments = new StringBuilder();
            commandArguments.AppendFormat("\"{0}\" \"{1}\"", bootstrapFile, encryptionKey);
            return commandArguments.ToString();
        }

        public static string PrepareConfigurationFile(string workingDirectory, IVariables variables, Script script)
        {
            var configurationFile = Path.Combine(workingDirectory, "Configure." + Guid.NewGuid().ToString().Substring(10) + ".sh");

            var builder = new StringBuilder(BootstrapScriptTemplate);

            var featureEnabled = FeatureToggle.BashParametersArrayFeatureToggle.IsEnabled(variables);
            builder.Replace("#### BashParametersArrayFeatureToggle ####", featureEnabled ? "true" : "false");

            var encryptedVariables = EncryptVariables(variables);
            builder.Replace("#### VariableDeclarations ####", string.Join(LinuxNewLine, GetVariableSwitchConditions(encryptedVariables)));
            if (featureEnabled)
            {
                var scriptUsesOctopusParameters = ScriptUsesOctopusParameters(script, variables);
                // If the script doesn't use octopus_parameters at all, we don't want to bloat the bootstrap script with unused values.
                if (scriptUsesOctopusParameters)
                {
                    var variableString = GetEncryptedVariablesKvp(variables);
                    builder.Replace("#### VARIABLESTRING.IV ####", variableString.iv);
                    builder.Replace("#### VARIABLESTRING.ENCRYPTED ####", variableString.encrypted);
                    builder.Replace("#### SCRIPT_USES_OCTOPUS_PARAMETERS ####", "true");
                }
                else
                {
                    builder.Replace("#### SCRIPT_USES_OCTOPUS_PARAMETERS ####", "false");
                }
            }
            else
            {
                // Feature toggle is off, so the placeholder won't be used but still needs to be replaced
                builder.Replace("#### SCRIPT_USES_OCTOPUS_PARAMETERS ####", "false");
            }

            using (var file = new FileStream(configurationFile, FileMode.CreateNew, FileAccess.Write))
            using (var writer = new StreamWriter(file, Encoding.ASCII))
            {
                writer.Write(builder.Replace(WindowsNewLine, LinuxNewLine));
                writer.Flush();
            }

            File.SetAttributes(configurationFile, FileAttributes.Hidden);
            return configurationFile;
        }

        static bool ScriptUsesOctopusParameters(Script script, IVariables variables)
        {
            // Check if script actually uses octopus_parameters array access
            // Pattern matches: octopus_parameters[key], octopus_parameters[@], etc.
            const string pattern = @"octopus_parameters\[";
            var regex = new System.Text.RegularExpressions.Regex(pattern);

            if (File.Exists(script.File) && regex.IsMatch(File.ReadAllText(script.File)))
                return true;

            foreach (var variableName in variables.GetNames().Where(ScriptVariables.IsLibraryScriptModule))
            {
                if (ScriptVariables.GetLibraryScriptModuleLanguage(variables, variableName) == ScriptSyntax.Bash)
                {
                    var contents = variables.Get(variableName);
                    if (contents != null && regex.IsMatch(contents))
                        return true;
                }
            }

            return false;
        }

        static (string encrypted, string iv) GetEncryptedVariablesKvp(IVariables variables)
        {
            var sb = new StringBuilder();
            foreach (var variable in variables
                         .Where(v => !ScriptVariables.IsLibraryScriptModule(v.Key))
                         .Where(v => !ScriptVariables.IsBuildInformationVariable(v.Key)))
            {
                sb.Append(variable.Key).Append('\0');
                sb.Append(variable.Value ?? "").Append('\0');
            }

            var encrypted = VariableEncryptor.Encrypt(sb.ToString());
            var rawEncrypted = AesEncryption.ExtractIV(encrypted, out var iv);
            return (
                Convert.ToBase64String(rawEncrypted),
                ToHex(iv)
            );
        }

        static IList<EncryptedVariable> EncryptVariables(IVariables variables)
        {
            return variables.GetNames()
                .Select(name =>
                {
                    var encryptedValue = VariableEncryptor.Encrypt(variables.Get(name) ?? "");
                    var raw = AesEncryption.ExtractIV(encryptedValue, out var iv);

                    return new EncryptedVariable(name, Convert.ToBase64String(raw), ToHex(iv));
                }).ToList();
        }

        static IEnumerable<string> GetVariableSwitchConditions(IEnumerable<EncryptedVariable> variables)
        {
            return variables
                .Select(variable =>
                {
                    var variableValue = $@"decrypt_variable ""{variable.EncryptedValue}"" ""{variable.Iv}""";
                    return string.Format("    \"{1}\"){0}   {2}   ;;{0}", Environment.NewLine, EncodeValue(variable.Name), variableValue);
                });
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
                writer.WriteLine("shift"); // Shift the variable decryption key out of scope of the user script (see: https://github.com/OctopusDeploy/Calamari/pull/773)
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
                if (ScriptVariables.GetLibraryScriptModuleLanguage(variables, variableName) == ScriptSyntax.Bash)
                {
                    var libraryScriptModuleName = ScriptVariables.GetLibraryScriptModuleName(variableName);
                    var name = ScriptVariables.FormatScriptName(libraryScriptModuleName); 
                    var moduleFileName = $"{name}.sh";
                    var moduleFilePath = Path.Combine(workingDirectory, moduleFileName);
                    Log.VerboseFormat("Writing script module '{0}' as bash script {1}. Import this via `source {1}`.", libraryScriptModuleName, moduleFileName, name);
                    Encoding utf8WithoutBom = new UTF8Encoding(false);
                    var contents = variables.Get(variableName);
                    if (contents == null)
                        throw new InvalidOperationException($"Value for variable {variableName} could not be found.");
                    CalamariFileSystem.OverwriteFile(moduleFilePath, contents, utf8WithoutBom);
                    EnsureValidUnixFile(moduleFilePath);
                    yield return moduleFilePath;
                }
        }

        class EncryptedVariable
        {
            public EncryptedVariable(string name, string encryptedValue, string iv)
            {
                Name = name;
                EncryptedValue = encryptedValue;
                Iv = iv;
            }

            public string Name { get; }
            public string EncryptedValue { get; }
            public string Iv { get; }
        }
    }
}