using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Features.Scripting.DotnetScript
{
    public static class DotnetScriptBootstrapper
    {
        static readonly string BootstrapScriptTemplate;
        static readonly string ClassBasedBootstrapScriptTemplate;
        static readonly string SensitiveVariablePassword = AesEncryption.RandomString(16);
        static readonly AesEncryption VariableEncryptor = AesEncryption.ForScripts(SensitiveVariablePassword);
        static readonly ICalamariFileSystem CalamariFileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
        static readonly Regex ScriptParameterArgumentsRegex = new Regex(@"^(?<scriptCommandArgs>.*)\s*--\s(?<scriptArgs>.*)$", RegexOptions.Compiled);
        static DotnetScriptBootstrapper()
        {
            BootstrapScriptTemplate = EmbeddedResource.ReadEmbeddedText(typeof(DotnetScriptBootstrapper).Namespace + ".Bootstrap.csx");
            ClassBasedBootstrapScriptTemplate = EmbeddedResource.ReadEmbeddedText(typeof(DotnetScriptBootstrapper).Namespace + ".ClassBootstrap.csx");
        }

        public static string? DotnetScriptPath(ICommandLineRunner commandLineRunner, Dictionary<string, string>? environmentVars)
        {
            // On Windows dotnet tools use the %USERPROFILE%\.dotnet\tools location. In Calamari the UserProfile is set to 
            // C:\Windows\system32\config\systemprofile, if the tool has been installed under another profile this will not find dotnet-script
            // This approach handles dotnet-script being installed via powershell/bash scripts or invoking the executable installed by dotnet-tools
            // directly.
            var dotnetScriptExecutorPath = typeof(DotnetScriptExecutor).Assembly.Location;
            var bundledPath = Path.GetDirectoryName(dotnetScriptExecutorPath);

            var executableNames = CalamariEnvironment.IsRunningOnWindows
                ? new[] { "dotnet-script.cmd", "dotnet-script.exe" }
                : new[] { "dotnet-script.sh", "dotnet-script", "dotnet-script.dll" };

            foreach (var executableName in executableNames)
            {
                var (_, commandOutput) = ExecuteCommandAndReturnOutput(commandLineRunner,
                                                                       environmentVars,
                                                                       CalamariEnvironment.IsRunningOnWindows ? "where" : "which",
                                                                       executableName);
                
                var hasDotnetScriptMessage = commandOutput.Messages
                                                          .Where(m => m.Level == Level.Verbose)
                                                          .FirstOrDefault(m => m.Text.Contains("dotnet-script") && 
                                                                               (bundledPath == null || !m.Text.Contains(bundledPath)));

                if (hasDotnetScriptMessage != null)
                {
                    return hasDotnetScriptMessage.Text;
                }
            }

            return null;
        }

        static (bool wasSuccessful, CaptureCommandOutput) ExecuteCommandAndReturnOutput(ICommandLineRunner commandLineRunner, Dictionary<string, string> envVars, string exe, params string[] arguments)
        {
            var captureCommandOutput = new CaptureCommandOutput();
            var invocation = new CommandLineInvocation(exe, arguments)
            {
                OutputAsVerbose = false,
                OutputToLog = false,
                AdditionalInvocationOutputSink = captureCommandOutput,
                EnvironmentVars = envVars,
                WorkingDirectory = Path.GetPathRoot(Environment.CurrentDirectory)
            };

            var res = commandLineRunner.Execute(invocation);

            return (res.ExitCode == 0, captureCommandOutput);
        }

        public static string FindBundledExecutable()
        {
            var exeName = $"dotnet-script.{(CalamariEnvironment.IsRunningOnWindows ? "cmd" : "dll")}";
            var myPath = typeof(DotnetScriptExecutor).Assembly.Location;
            var parent = Path.GetDirectoryName(myPath);
            var executable = Path.GetFullPath(Path.Combine(parent, "dotnet-script", exeName));

            if (File.Exists(executable))
                return executable;

            throw new CommandException(string.Format("dotnet-script was not found at '{0}'", executable));
        }

        public static string FormatCommandArguments(string bootstrapFile, string? scriptParameters)
        {
            var (scriptCommandArguments, scriptArguments) = RetrieveParameterValues(scriptParameters);
            var encryptionKey = Convert.ToBase64String(VariableEncryptor.EncryptionKey);
            var commandArguments = new StringBuilder();
            commandArguments.Append("-s https://api.nuget.org/v3/index.json ");
            if (!string.IsNullOrWhiteSpace(scriptCommandArguments)) commandArguments.Append($"{scriptCommandArguments} ");
            commandArguments.AppendFormat("\"{0}\" -- {1} \"{2}\"", bootstrapFile, scriptArguments, encryptionKey);
            return commandArguments.ToString();
        }

        [return: NotNullIfNotNull("scriptParameters")]
        static (string? scriptCommandArguments, string? scriptArguments) RetrieveParameterValues(string? scriptParameters)
        {
            var scriptCommandArguments = string.Empty;
            if (!string.IsNullOrEmpty(scriptParameters))
            {
                var scriptParameterParts = ScriptParameterArgumentsRegex.Match(scriptParameters);
                if (scriptParameterParts.Success)
                {
                    scriptCommandArguments = scriptParameterParts.Groups["scriptCommandArgs"].Value;
                    scriptParameters = scriptParameterParts.Groups["scriptArgs"].Value;
                }
            }

            return (scriptCommandArguments.Trim(), scriptParameters?.Trim().TrimStart('-').Trim());
        }

        public static (string bootstrapFile, string[] temporaryFiles) PrepareBootstrapFile(string scriptFilePath, string configurationFile, string workingDirectory, IVariables variables)
        {
            var bootstrapFile = Path.Combine(workingDirectory, "Bootstrap." + Guid.NewGuid().ToString().Substring(10) + "." + Path.GetFileName(scriptFilePath));
            var scriptModulePaths = PrepareScriptModules(variables, workingDirectory).ToArray();

            using (var file = new FileStream(bootstrapFile, FileMode.CreateNew, FileAccess.Write))
            using (var writer = new StreamWriter(file, Encoding.UTF8))
            {
                writer.WriteLine("#load \"" + configurationFile.Replace("\\", "\\\\") + "\"");
                writer.WriteLine("#load \"" + scriptFilePath.Replace("\\", "\\\\") + "\"");

                writer.Flush();
            }

            File.SetAttributes(bootstrapFile, FileAttributes.Hidden);
            return (bootstrapFile, scriptModulePaths);
        }

        static IEnumerable<string> PrepareScriptModules(IVariables variables, string workingDirectory)
        {
            foreach (var variableName in variables.GetNames().Where(ScriptVariables.IsLibraryScriptModule))
                if (ScriptVariables.GetLibraryScriptModuleLanguage(variables, variableName) == ScriptSyntax.CSharp)
                {
                    var libraryScriptModuleName = ScriptVariables.GetLibraryScriptModuleName(variableName);
                    var name = ScriptVariables.FormatScriptName(libraryScriptModuleName); 
                    var moduleFileName = $"{name}.csx";
                    var moduleFilePath = Path.Combine(workingDirectory, moduleFileName);
                    Log.VerboseFormat("Writing script module '{0}' as c# module {1}. Import this module via `#load \"{1}\"`.", libraryScriptModuleName, moduleFileName, name);
                    var contents = variables.Get(variableName);
                    if (contents == null)
                        throw new InvalidOperationException($"Value for variable {variableName} could not be found.");
                    CalamariFileSystem.OverwriteFile(moduleFilePath, contents, Encoding.UTF8);
                    yield return moduleFileName;
                }
        }

        public static string PrepareConfigurationFile(string workingDirectory, IVariables variables)
        {
            var configurationFile = Path.Combine(workingDirectory, "Configure." + Guid.NewGuid().ToString().Substring(10) + ".csx");
            bool.TryParse(variables.Get("Octopus.Action.Script.CSharp.UseOctopusClassBootstrapper", "false"), out var useClassBootstrapped);
            var builder = useClassBootstrapped
                ? new StringBuilder(ClassBasedBootstrapScriptTemplate)
                : new StringBuilder(BootstrapScriptTemplate);
            builder.Replace("/*{{VariableDeclarations}}*/", WriteVariableDictionary(variables));

            using (var file = new FileStream(configurationFile, FileMode.CreateNew, FileAccess.Write))
            using (var writer = new StreamWriter(file, Encoding.UTF8))
            {
                writer.Write(builder.ToString());
                writer.Flush();
            }

            File.SetAttributes(configurationFile, FileAttributes.Hidden);
            return configurationFile;
        }

        static string WriteVariableDictionary(IVariables variables)
        {
            var builder = new StringBuilder();
            foreach (var variable in variables.GetNames())
            {
                var variableValue = EncryptVariable(variables.Get(variable));
                builder.Append("\t\t\tthis[").Append(EncodeValue(variable)).Append("] = ").Append(variableValue).AppendLine(";");
            }

            return builder.ToString();
        }

        static string EncodeValue(string value)
        {
            if (value == null)
                return "null;";

            var bytes = Encoding.UTF8.GetBytes(value);
            return $"System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(\"{Convert.ToBase64String(bytes)}\"))";
        }

        static string EncryptVariable(string? value)
        {
            if (value == null)
                return "null;";

            var encrypted = VariableEncryptor.Encrypt(value);
            var rawEncrypted = AesEncryption.ExtractIV(encrypted, out var iv);

            return $@"DecryptString(""{Convert.ToBase64String(rawEncrypted)}"", ""{Convert.ToBase64String(iv)}"")";
        }
    }
}