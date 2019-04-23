using System;
using System.IO;
using System.Linq;
using System.Text;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Util;
using Octostache;

namespace Calamari.Integration.Scripting.WindowsPowerShell
{
    public class PowerShellBootstrapper
    {
        static string powerShellPath;
        const string EnvPowerShellPath = "PowerShell.exe";
        private static readonly string BootstrapScriptTemplate;
        private static readonly string DebugBootstrapScriptTemplate;
        static readonly string SensitiveVariablePassword = AesEncryption.RandomString(16);
        static readonly AesEncryption VariableEncryptor = new AesEncryption(SensitiveVariablePassword);
        static readonly ICalamariFileSystem CalamariFileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();

        static PowerShellBootstrapper()
        {
            BootstrapScriptTemplate = EmbeddedResource.ReadEmbeddedText(typeof (PowerShellBootstrapper).Namespace + ".Bootstrap.ps1");
            DebugBootstrapScriptTemplate = EmbeddedResource.ReadEmbeddedText(typeof (PowerShellBootstrapper).Namespace + ".DebugBootstrap.ps1");
        }

        public static string PathToPowerShellExecutable()
        {
            if (powerShellPath != null)
            {
                return powerShellPath;
            }

            try
            {
                var systemFolder = Environment.GetFolderPath(Environment.SpecialFolder.System);
                powerShellPath = Path.Combine(systemFolder, @"WindowsPowershell\v1.0\", EnvPowerShellPath);

                if (!File.Exists(powerShellPath))
                {
                    powerShellPath = EnvPowerShellPath;
                }
            }
            catch (Exception)
            {
                powerShellPath = EnvPowerShellPath;
            }

            return powerShellPath;
        }

        public static string FormatCommandArguments(string bootstrapFile, string debuggingBootstrapFile, CalamariVariableDictionary variables)
        {
            var encryptionKey = Convert.ToBase64String(AesEncryption.GetEncryptionKey(SensitiveVariablePassword));
            var commandArguments = new StringBuilder();
            var customPowerShellVersion = variables[SpecialVariables.Action.PowerShell.CustomPowerShellVersion];
            if (!string.IsNullOrEmpty(customPowerShellVersion))
            {
                commandArguments.Append($"-Version {customPowerShellVersion} ");
            }
            var executeWithoutProfile = variables[SpecialVariables.Action.PowerShell.ExecuteWithoutProfile];
            bool noProfile;
            if (bool.TryParse(executeWithoutProfile, out noProfile) && noProfile)
            {
                commandArguments.Append("-NoProfile ");
            }
            commandArguments.Append("-NoLogo ");
            commandArguments.Append("-NonInteractive ");
            commandArguments.Append("-ExecutionPolicy Unrestricted ");

            var filetoExecute = IsDebuggingEnabled(variables)
                ? debuggingBootstrapFile.EscapeSingleQuotedString()
                : bootstrapFile.EscapeSingleQuotedString();

            commandArguments.AppendFormat("-Command \"Try {{. {{. '{0}' -OctopusKey '{1}'; if ((test-path variable:global:lastexitcode)) {{ exit $LastExitCode }}}};}} catch {{ throw }}\"", filetoExecute, encryptionKey);
            return commandArguments.ToString();
        }

        private static bool IsDebuggingEnabled(CalamariVariableDictionary variables)
        {
            var powershellDebugMode = variables[SpecialVariables.Action.PowerShell.DebugMode];

            if (string.IsNullOrEmpty(powershellDebugMode))
                return false;
            if (powershellDebugMode.Equals("False", StringComparison.OrdinalIgnoreCase) || powershellDebugMode.Equals("None", StringComparison.OrdinalIgnoreCase))
                return false;
            return true;
        }

        public static string PrepareBootstrapFile(Script script, CalamariVariableDictionary variables)
        {
            var parent = Path.GetDirectoryName(Path.GetFullPath(script.File));
            var name = Path.GetFileName(script.File);
            var bootstrapFile = Path.Combine(parent, "Bootstrap." + name);
            var variableString = GetEncryptedVariablesString(variables);

            var builder = new StringBuilder(BootstrapScriptTemplate);
            builder.Replace("{{TargetScriptFile}}", script.File.EscapeSingleQuotedString())
                .Replace("{{ScriptParameters}}", script.Parameters)
                .Replace("{{EncryptedVariablesString}}", variableString.encrypted)
                .Replace("{{VariablesIV}}", variableString.iv)
                .Replace("{{LocalVariableDeclarations}}", DeclareLocalVariables(variables))
                .Replace("{{ScriptModules}}", DeclareScriptModules(variables, parent));

            builder = SetupDebugBreakpoints(builder, variables);

            CalamariFileSystem.OverwriteFile(bootstrapFile, builder.ToString(), new UTF8Encoding(true));

            File.SetAttributes(bootstrapFile, FileAttributes.Hidden);
            return bootstrapFile;
        }

        private static StringBuilder SetupDebugBreakpoints(StringBuilder builder, CalamariVariableDictionary variables)
        {
            const string powershellWaitForDebuggerCommand = "Wait-Debugger";
            var startOfBootstrapScriptDebugLocation = string.Empty;
            var beforeVariablesDebugLocation = string.Empty;
            var beforeScriptModulesDebugLocation = string.Empty;
            var beforeLaunchingUserScriptDebugLocation = string.Empty;

            var powershellDebugMode = variables[SpecialVariables.Action.PowerShell.DebugMode];
            if (!string.IsNullOrEmpty(powershellDebugMode))
            {
                switch (powershellDebugMode.ToLower())
                {
                    case "breakatstartofbootstrapscript":
                        startOfBootstrapScriptDebugLocation = powershellWaitForDebuggerCommand;
                        break;
                    case "breakbeforesettingvariables":
                        beforeVariablesDebugLocation = powershellWaitForDebuggerCommand;
                        break;
                    case "breakbeforeimportingscriptmodules":
                        beforeScriptModulesDebugLocation = powershellWaitForDebuggerCommand;
                        break;
                    case "breakbeforelaunchinguserscript":
                    case "true":
                        beforeLaunchingUserScriptDebugLocation = powershellWaitForDebuggerCommand;
                        break;
                }
            }
            builder.Replace("{{StartOfBootstrapScriptDebugLocation}}", startOfBootstrapScriptDebugLocation)
                .Replace("{{BeforeVariablesDebugLocation}}", beforeVariablesDebugLocation)
                .Replace("{{BeforeScriptModulesDebugLocation}}", beforeScriptModulesDebugLocation)
                .Replace("{{BeforeLaunchingUserScriptDebugLocation}}", beforeLaunchingUserScriptDebugLocation);

            return builder;
        }

        public static string PrepareDebuggingBootstrapFile(Script script)
        {
            var parent = Path.GetDirectoryName(Path.GetFullPath(script.File));
            var name = Path.GetFileName(script.File);
            var debugBootstrapFile = Path.Combine(parent, "DebugBootstrap." + name);
            var bootstrapFile = Path.Combine(parent, "Bootstrap." + name);
            var escapedBootstrapFile = bootstrapFile.EscapeSingleQuotedString();

            var builder = new StringBuilder(DebugBootstrapScriptTemplate);
            builder.Replace("{{BootstrapFile}}", escapedBootstrapFile);

            CalamariFileSystem.OverwriteFile(debugBootstrapFile, builder.ToString(), new UTF8Encoding(true));

            File.SetAttributes(debugBootstrapFile, FileAttributes.Hidden);
            return debugBootstrapFile;
        }


        static string DeclareScriptModules(CalamariVariableDictionary variables, string parentDirectory)
        {
            var output = new StringBuilder();

            WriteScriptModules(variables, parentDirectory, output);

            return output.ToString();
        }

        static void WriteScriptModules(VariableDictionary variables, string parentDirectory, StringBuilder output)
        {
            foreach (var variableName in variables.GetNames().Where(SpecialVariables.IsLibraryScriptModule))
            {
                if (SpecialVariables.GetLibraryScriptModuleLangauge(variables, variableName) == ScriptSyntax.PowerShell) {
                    var libraryScriptModuleName = SpecialVariables.GetLibraryScriptModuleName(variableName);
                    var name = "Library_" + new string(libraryScriptModuleName.Where(char.IsLetterOrDigit).ToArray()) + "_" + DateTime.Now.Ticks;
                    var moduleFileName = $"{name}.psm1";
                    var moduleFilePath = Path.Combine(parentDirectory, moduleFileName);
                    Log.VerboseFormat("Writing script module '{0}' as PowerShell module {1}. This module will be automatically imported - functions will automatically be in scope.", libraryScriptModuleName, moduleFileName, name);
                    CalamariFileSystem.OverwriteFile(moduleFilePath, variables.Get(variableName), Encoding.UTF8);
                    output.AppendLine($"Import-ScriptModule '{libraryScriptModuleName.EscapeSingleQuotedString()}' '{moduleFilePath.EscapeSingleQuotedString()}'");
                    output.AppendLine();
                }
            }
        }

        static (string encrypted, string iv) GetEncryptedVariablesString(CalamariVariableDictionary variables)
        {
            var sb = new StringBuilder();
            foreach (var variableName in variables.GetNames().Where(name => !SpecialVariables.IsLibraryScriptModule(name)))
            {
                var value = variables.Get(variableName);
                var encryptedValue = value == null ? "nul" : EncodeAsBase64(value); // "nul" is not a valid Base64 string
                sb.Append(EncodeAsBase64(variableName)).Append("$").AppendLine(encryptedValue);
            }

            var encrypted = VariableEncryptor.Encrypt(sb.ToString());
            var rawEncrypted = AesEncryption.ExtractIV(encrypted, out var iv);
            return (
                Convert.ToBase64String(rawEncrypted, Base64FormattingOptions.InsertLineBreaks),
                Convert.ToBase64String(iv)
            );
        }

        static string DeclareLocalVariables(CalamariVariableDictionary variables)
        {
            var output = new StringBuilder();
            foreach (var variableName in variables.GetNames().Where(name => !SpecialVariables.IsLibraryScriptModule(name)))
            {
                if (SpecialVariables.IsExcludedFromLocalVariables(variableName))
                {
                    continue;
                }

                // This is the way we used to fix up the identifiers - people might still rely on this behavior
                var legacyKey = new string(variableName.Where(char.IsLetterOrDigit).ToArray());

                // This is the way we should have done it
                var smartKey = new string(variableName.Where(IsValidPowerShellIdentifierChar).ToArray());

                if (legacyKey != smartKey)
                {
                    WriteVariableAssignment(output, legacyKey, variableName);
                }

                WriteVariableAssignment(output, smartKey, variableName);
            }

            return output.ToString();
        }

        static void WriteVariableAssignment(StringBuilder writer, string key, string variableName)
        {
            if (string.IsNullOrWhiteSpace(key)) // we can end up with an empty key if everything was stripped by the IsValidPowerShellIdentifierChar check in WriteLocalVariables
                return;
            writer.Append("if (-Not (test-path variable:global:").Append(key).AppendLine(")) {");
            writer.Append("  ${").Append(key).Append("} = $OctopusParameters[").Append(EncodeValue(variableName)).AppendLine("]");
            writer.AppendLine("}");
        }

        static string EncodeValue(string value)
        {
            if (value == null)
                return "$null";

            var bytes = Encoding.UTF8.GetBytes(value);
            return string.Format("[System.Text.Encoding]::UTF8.GetString(" + "[Convert]::FromBase64String(\"{0}\")" + ")", Convert.ToBase64String(bytes));
        }
        
        static string EncodeAsBase64(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            return Convert.ToBase64String(bytes);
        }

        static bool IsValidPowerShellIdentifierChar(char c)
        {
            return c == '_' || char.IsLetterOrDigit(c);
        }
    }
}