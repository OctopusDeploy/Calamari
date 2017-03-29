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
                var systemFolder = CrossPlatform.GetSystemFolderPath();
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
                ? debuggingBootstrapFile.Replace("'", "''")
                : bootstrapFile.Replace("'", "''");

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

            var builder = new StringBuilder(BootstrapScriptTemplate);
            builder.Replace("{{TargetScriptFile}}", script.File.Replace("'", "''"))
                    .Replace("{{ScriptParameters}}", script.Parameters)
                    .Replace("{{VariableDeclarations}}", DeclareVariables(variables))
                    .Replace("{{ScriptModules}}", DeclareScriptModules(variables));

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
                        beforeLaunchingUserScriptDebugLocation = powershellWaitForDebuggerCommand;
                        break;
                    default:
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
            var escapedBootstrapFile = bootstrapFile.Replace("'", "''");

            var builder = new StringBuilder(DebugBootstrapScriptTemplate);
            builder.Replace("{{BootstrapFile}}", escapedBootstrapFile);

            CalamariFileSystem.OverwriteFile(debugBootstrapFile, builder.ToString(), new UTF8Encoding(true));

            File.SetAttributes(debugBootstrapFile, FileAttributes.Hidden);
            return debugBootstrapFile;
        }

        static string DeclareVariables(CalamariVariableDictionary variables)
        {
            var output = new StringBuilder();

            WriteVariableDictionary(variables, output);
            output.AppendLine();
            WriteLocalVariables(variables, output);

            return output.ToString();
        }

        static string DeclareScriptModules(CalamariVariableDictionary variables)
        {
            var output = new StringBuilder();

            WriteScriptModules(variables, output);

            return output.ToString();
        }

        static void WriteScriptModules(VariableDictionary variables, StringBuilder output)
        {
            foreach (var variableName in variables.GetNames().Where(SpecialVariables.IsLibraryScriptModule))
            {
                var name = "Library_" + new string(SpecialVariables.GetLibraryScriptModuleName(variableName).Where(char.IsLetterOrDigit).ToArray()) + "_" + DateTime.Now.Ticks;
                output.Append("New-Module -Name ").Append(name).Append(" -ScriptBlock {");
                output.AppendLine(variables.Get(variableName));
                output.AppendLine("} | Import-Module");
                output.AppendLine();
            }
        }

        static void WriteVariableDictionary(CalamariVariableDictionary variables, StringBuilder output)
        {
            output.AppendLine("$OctopusParameters = New-Object 'System.Collections.Generic.Dictionary[String,String]' (,[System.StringComparer]::OrdinalIgnoreCase)");
            foreach (var variableName in variables.GetNames().Where(name => !SpecialVariables.IsLibraryScriptModule(name)))
            {
                var variableValue = variables.IsSensitive(variableName)
                    ? EncryptVariable(variables.Get(variableName))
                    : EncodeValue(variables.Get(variableName));

                output.Append("$OctopusParameters[").Append(EncodeValue(variableName)).Append("] = ").AppendLine(variableValue);
            }
        }

        static void WriteLocalVariables(CalamariVariableDictionary variables, StringBuilder output)
        {
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
        }

        static void WriteVariableAssignment(StringBuilder writer, string key, string variableName)
        {
            if (string.IsNullOrWhiteSpace(key)) // we can end up with an empty key if everything was stripped by the IsValidPowerShellIdentifierChar check in WriteLocalVariables
                return;
            writer.Append("if (-Not (test-path variable:global:").Append(key).AppendLine(")) {");
            writer.Append("  ${").Append(key).Append("} = $OctopusParameters[").Append(EncodeValue(variableName)).AppendLine("]");
            writer.AppendLine("}");
        }

        static string EncryptVariable(string value)
        {
            if (value == null)
                return "$null";

            var encrypted = VariableEncryptor.Encrypt(value);
            byte[] iv;
            var rawEncrypted = AesEncryption.ExtractIV(encrypted, out iv);
            // The seemingly superfluous '-as' below was for PowerShell 2.0.  Without it, a cast exception was thrown when trying to add the object
            // to a generic collection. 
            return string.Format("(Decrypt-String \"{0}\" \"{1}\") -as [string]", Convert.ToBase64String(rawEncrypted), Convert.ToBase64String(iv));
        }

        static string EncodeValue(string value)
        {
            if (value == null)
                return "$null";

            var bytes = Encoding.UTF8.GetBytes(value);
            return string.Format("[System.Text.Encoding]::UTF8.GetString(" + "[Convert]::FromBase64String(\"{0}\")" + ")", Convert.ToBase64String(bytes));
        }

        static bool IsValidPowerShellIdentifierChar(char c)
        {
            return c == '_' || char.IsLetterOrDigit(c);
        }
    }
}
