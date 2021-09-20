using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Features.Scripting.WindowsPowerShell
{
    public class WindowsPowerShellBootstrapper : PowerShellBootstrapper
    {
        const string EnvPowerShellPath = "PowerShell.exe";
        static string? powerShellPath;

        public override bool AllowImpersonation()
        {
            return true;
        }

        public override string PathToPowerShellExecutable(IVariables variables)
        {
            if (powerShellPath != null)
                return powerShellPath;

            try
            {
                var systemFolder = Environment.GetFolderPath(Environment.SpecialFolder.System);
                powerShellPath = Path.Combine(systemFolder, @"WindowsPowershell\v1.0\", EnvPowerShellPath);

                if (!File.Exists(powerShellPath))
                    powerShellPath = EnvPowerShellPath;
            }
            catch (Exception)
            {
                powerShellPath = EnvPowerShellPath;
            }

            return powerShellPath;
        }

        protected override IEnumerable<string> ContributeCommandArguments(IVariables variables)
        {
            var customPowerShellVersion = variables[PowerShellVariables.CustomPowerShellVersion];
            if (!string.IsNullOrEmpty(customPowerShellVersion))
                yield return $"-Version {customPowerShellVersion} ";
        }
    }

    public class UnixLikePowerShellCoreBootstrapper : PowerShellCoreBootstrapper
    {
        public override bool AllowImpersonation()
        {
            return false;
        }

        public override string PathToPowerShellExecutable(IVariables variables)
        {
            return "pwsh";
        }
    }

    public class WindowsPowerShellCoreBootstrapper : PowerShellCoreBootstrapper
    {
        const string EnvPowerShellPath = "pwsh.exe";
        readonly ICalamariFileSystem fileSystem;

        public WindowsPowerShellCoreBootstrapper(ICalamariFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        public override bool AllowImpersonation()
        {
            return true;
        }

        public override string PathToPowerShellExecutable(IVariables variables)
        {
            var customVersion = variables[PowerShellVariables.CustomPowerShellVersion];
            try
            {
                var availablePowerShellVersions = new[]
                    {
                        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
                    }
                    .Where(p => p != null)
                    .Distinct()
                    .Select(pf => Path.Combine(pf, "PowerShell"))
                    .Where(fileSystem.DirectoryExists)
                    .SelectMany(fileSystem.EnumerateDirectories)
                    .Select<string, (string path, string versionId, int? majorVersion, string remaining)>(d =>
                    {
                        var directoryName = fileSystem.GetDirectoryName(d);

                        // Directories are typically versions like "6" or they might also have a prerelease component like "7-preview"
                        var splitString = directoryName.Split(new[] { '-' }, 2);
                        var majorVersionPart = splitString[0];
                        var preRelease =
                            splitString.Length < 2
                                ? string.Empty
                                : splitString[1]; // typically a prerelease tag, like "preview"

                        if (int.TryParse(majorVersionPart, out var majorVersion))
                            return (d, directoryName, majorVersion, preRelease);
                        return (d, directoryName, null, preRelease);
                    })
                    .ToList();

                var latestPowerShellVersionDirectory = availablePowerShellVersions
                    .Where(p => string.IsNullOrEmpty(customVersion) || p.versionId == customVersion)
                    .OrderByDescending(p => p.majorVersion)
                    .ThenBy(p => p.remaining)
                    .Select(p => p.path)
                    .FirstOrDefault();

                if (!string.IsNullOrEmpty(customVersion) && latestPowerShellVersionDirectory == null)
                    throw new PowerShellVersionNotFoundException(customVersion, availablePowerShellVersions.Select(v => v.versionId));

                if (latestPowerShellVersionDirectory == null)
                    return EnvPowerShellPath;

                var pathToPwsh = Path.Combine(latestPowerShellVersionDirectory, EnvPowerShellPath);

                return fileSystem.FileExists(pathToPwsh) ? pathToPwsh : EnvPowerShellPath;
            }
            catch (PowerShellVersionNotFoundException)
            {
                throw;
            }
            catch (Exception)
            {
                return EnvPowerShellPath;
            }
        }
    }

    public abstract class PowerShellCoreBootstrapper : PowerShellBootstrapper
    {
        protected override IEnumerable<string> ContributeCommandArguments(IVariables variables)
        {
            yield break;
        }
    }

    public class PowerShellVersionNotFoundException : CommandException
    {
        public PowerShellVersionNotFoundException(string customVersion, IEnumerable<string> availableVersions)
            : base($"Attempted to use version '{customVersion}' of PowerShell Core, but this version could not be found. Available versions: {string.Join(", ", availableVersions)}")
        {
        }
    }

    public abstract class PowerShellBootstrapper
    {
        static readonly string BootstrapScriptTemplate;
        static readonly string DebugBootstrapScriptTemplate;
        static readonly string SensitiveVariablePassword = AesEncryption.RandomString(16);
        static readonly AesEncryption VariableEncryptor = new AesEncryption(SensitiveVariablePassword);
        static readonly ICalamariFileSystem CalamariFileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();

        static PowerShellBootstrapper()
        {
            BootstrapScriptTemplate = EmbeddedResource.ReadEmbeddedText(typeof(PowerShellBootstrapper).Namespace + ".Bootstrap.ps1");
            DebugBootstrapScriptTemplate = EmbeddedResource.ReadEmbeddedText(typeof(PowerShellBootstrapper).Namespace + ".DebugBootstrap.ps1");
        }

        public abstract bool AllowImpersonation();

        public abstract string PathToPowerShellExecutable(IVariables variables);

        public string FormatCommandArguments(string bootstrapFile, string debuggingBootstrapFile, IVariables variables)
        {
            var encryptionKey = Convert.ToBase64String(AesEncryption.GetEncryptionKey(SensitiveVariablePassword));
            var commandArguments = new StringBuilder();
            var executeWithoutProfile = variables[PowerShellVariables.ExecuteWithoutProfile];
            var traceCommand = GetPsDebugCommand(variables);
            var displayProgressCommand = GetDisplayProgressCommand(variables);

            foreach (var argument in ContributeCommandArguments(variables))
                commandArguments.Append(argument);

            bool noProfile;
            if (bool.TryParse(executeWithoutProfile, out noProfile) && noProfile)
                commandArguments.Append("-NoProfile ");
            commandArguments.Append("-NoLogo ");
            commandArguments.Append("-NonInteractive ");
            commandArguments.Append("-ExecutionPolicy Unrestricted ");

            var fileToExecute = IsDebuggingEnabled(variables)
                ? debuggingBootstrapFile.EscapeSingleQuotedString()
                : bootstrapFile.EscapeSingleQuotedString();

            commandArguments.AppendFormat("-Command \"{0} {1}Try {{. {{. '{2}' -OctopusKey '{3}'; if ((test-path variable:global:lastexitcode)) {{ exit $LastExitCode }}}};}} catch {{ throw }}\"", displayProgressCommand, traceCommand, fileToExecute, encryptionKey);
            return commandArguments.ToString();
        }

        static string GetDisplayProgressCommand(IVariables variables)
        {
            var powerShellProgressPreferenceArg = variables[PowerShellVariables.OutputPowerShellProgress];
            
            int.TryParse(powerShellProgressPreferenceArg, out var powerShellProgressPreferenceArgAsInt);
            bool.TryParse(powerShellProgressPreferenceArg, out var powerShellProgressPreferenceArgAsBool);
            if (powerShellProgressPreferenceArgAsInt > 0 || powerShellProgressPreferenceArgAsBool)
            {
                return "$ProgressPreference = 'Continue';";
            }
            return "$ProgressPreference = 'SilentlyContinue';";
        }

        static string GetPsDebugCommand(IVariables variables)
        {
            //https://docs.microsoft.com/en-us/powershell/module/microsoft.powershell.core/set-psdebug?view=powershell-6
            var traceArg = variables[PowerShellVariables.PSDebug.Trace];
            var traceCommand = "-Trace 0";
            int.TryParse(traceArg, out var traceArgAsInt);
            bool.TryParse(traceArg, out var traceArgAsBool);
            if (traceArgAsInt > 0 || traceArgAsBool)
            {
                var powerShellVersion = ScriptingEnvironment.SafelyGetPowerShellVersion();

                if (powerShellVersion.Major < 5 && powerShellVersion.Major > 0)
                {
                    Log.Warn($"{PowerShellVariables.PSDebug.Trace} is enabled, but PowerShell tracing is only supported with PowerShell versions 5 and above. This server is currently running PowerShell version {powerShellVersion}.");
                }
                else
                {
                    Log.Warn($"{PowerShellVariables.PSDebug.Trace} is enabled. This should only be used for debugging, and then disabled again for normal deployments.");
                    if (traceArgAsInt > 0)
                        traceCommand = $"-Trace {traceArgAsInt}";
                    if (traceArgAsBool)
                        traceCommand = "-Trace 2";
                }
            }

            var strictArg = variables[PowerShellVariables.PSDebug.Strict];
            var strictCommand = "";
            if (bool.TryParse(strictArg, out var strictArgAsBool) && strictArgAsBool)
            {
                Log.Info($"{PowerShellVariables.PSDebug.Strict} is enabled, putting PowerShell into strict mode where variables must be assigned a value before being referenced in a script. If a variable is referenced before a value is assigned, an exception will be thrown. This feature is experimental.");
                strictCommand = " -Strict";
            }

            return $"Set-PSDebug {traceCommand}{strictCommand};";
        }

        protected abstract IEnumerable<string> ContributeCommandArguments(IVariables variables);

        static bool IsDebuggingEnabled(IVariables variables)
        {
            var powershellDebugMode = variables[PowerShellVariables.DebugMode];

            if (string.IsNullOrEmpty(powershellDebugMode))
                return false;
            if (powershellDebugMode.Equals("False", StringComparison.OrdinalIgnoreCase) || powershellDebugMode.Equals("None", StringComparison.OrdinalIgnoreCase))
                return false;
            return true;
        }

        public (string bootstrapFile, string[] temporaryFiles) PrepareBootstrapFile(Script script, IVariables variables)
        {
            var parent = Path.GetDirectoryName(Path.GetFullPath(script.File));
            var name = Path.GetFileName(script.File);
            var bootstrapFile = Path.Combine(parent, "Bootstrap." + name);
            var variableString = GetEncryptedVariablesString(variables);

            var (scriptModulePaths, scriptModuleDeclarations) = DeclareScriptModules(variables, parent);

            var builder = new StringBuilder(BootstrapScriptTemplate);
            builder.Replace("{{TargetScriptFile}}", script.File.EscapeSingleQuotedString())
                .Replace("{{ScriptParameters}}", script.Parameters)
                .Replace("{{EncryptedVariablesString}}", variableString.encrypted)
                .Replace("{{VariablesIV}}", variableString.iv)
                .Replace("{{LocalVariableDeclarations}}", DeclareLocalVariables(variables))
                .Replace("{{ScriptModules}}", scriptModuleDeclarations);

            builder = SetupDebugBreakpoints(builder, variables);

            CalamariFileSystem.OverwriteFile(bootstrapFile, builder.ToString(), new UTF8Encoding(true));

            File.SetAttributes(bootstrapFile, FileAttributes.Hidden);
            return (bootstrapFile, scriptModulePaths);
        }

        static StringBuilder SetupDebugBreakpoints(StringBuilder builder, IVariables variables)
        {
            const string powershellWaitForDebuggerCommand = "Wait-Debugger";
            var startOfBootstrapScriptDebugLocation = string.Empty;
            var beforeVariablesDebugLocation = string.Empty;
            var beforeScriptModulesDebugLocation = string.Empty;
            var beforeLaunchingUserScriptDebugLocation = string.Empty;

            var powershellDebugMode = variables[PowerShellVariables.DebugMode];
            if (!string.IsNullOrEmpty(powershellDebugMode))
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

            builder.Replace("{{StartOfBootstrapScriptDebugLocation}}", startOfBootstrapScriptDebugLocation)
                .Replace("{{BeforeVariablesDebugLocation}}", beforeVariablesDebugLocation)
                .Replace("{{BeforeScriptModulesDebugLocation}}", beforeScriptModulesDebugLocation)
                .Replace("{{BeforeLaunchingUserScriptDebugLocation}}", beforeLaunchingUserScriptDebugLocation);

            return builder;
        }

        public string PrepareDebuggingBootstrapFile(Script script)
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

        static (string[] scriptModulePaths, string scriptModuleDeclarations) DeclareScriptModules(IVariables variables, string parentDirectory)
        {
            var output = new StringBuilder();

            var scriptModules = WriteScriptModules(variables, parentDirectory, output);

            return (scriptModules, output.ToString());
        }

        static string[] WriteScriptModules(IVariables variables, string parentDirectory, StringBuilder output)
        {
            var scriptModules = new List<string>();
            foreach (var variableName in variables.GetNames().Where(ScriptVariables.IsLibraryScriptModule))
                if (ScriptVariables.GetLibraryScriptModuleLanguage(variables, variableName) == ScriptSyntax.PowerShell)
                {
                    var libraryScriptModuleName = ScriptVariables.GetLibraryScriptModuleName(variableName);
                    var name = "Library_" + new string(libraryScriptModuleName.Where(char.IsLetterOrDigit).ToArray()) + "_" + DateTime.Now.Ticks;
                    var moduleFileName = $"{name}.psm1";
                    var moduleFilePath = Path.Combine(parentDirectory, moduleFileName);
                    Log.VerboseFormat("Writing script module '{0}' as PowerShell module {1}. This module will be automatically imported - functions will automatically be in scope.", libraryScriptModuleName, moduleFileName, name);
                    var contents = variables.Get(variableName);
                    if (contents == null)
                        throw new InvalidOperationException($"Value for variable {variableName} could not be found.");
                    CalamariFileSystem.OverwriteFile(moduleFilePath, contents, Encoding.UTF8);
                    output.AppendLine($"Import-ScriptModule '{libraryScriptModuleName.EscapeSingleQuotedString()}' '{moduleFilePath.EscapeSingleQuotedString()}'");
                    output.AppendLine();
                    scriptModules.Add(moduleFilePath);
                }

            return scriptModules.ToArray();
        }

        static (string encrypted, string iv) GetEncryptedVariablesString(IVariables variables)
        {
            var sb = new StringBuilder();
            foreach (var variableName in variables.GetNames().Where(name => !ScriptVariables.IsLibraryScriptModule(name)))
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

        static string DeclareLocalVariables(IVariables variables)
        {
            var output = new StringBuilder();
            foreach (var variableName in variables.GetNames().Where(name => !ScriptVariables.IsLibraryScriptModule(name)))
            {
                if (ScriptVariables.IsExcludedFromLocalVariables(variableName))
                    continue;

                // This is the way we used to fix up the identifiers - people might still rely on this behavior
                var legacyKey = new string(variableName.Where(char.IsLetterOrDigit).ToArray());

                // This is the way we should have done it
                var smartKey = new string(variableName.Where(IsValidPowerShellIdentifierChar).ToArray());

                if (legacyKey != smartKey)
                    WriteVariableAssignment(output, legacyKey, variableName);

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