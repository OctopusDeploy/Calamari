using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Proxies;
using Calamari.Common.Plumbing.Variables;
using Calamari.Terraform.Helpers;
using NuGet.Versioning;

namespace Calamari.Terraform
{
    class TerraformCliExecutor : IDisposable
    {
        readonly ILog log;
        readonly ICalamariFileSystem fileSystem;
        readonly ICommandLineRunner commandLineRunner;
        readonly RunningDeployment deployment;
        readonly IVariables variables;
        readonly Dictionary<string, string> environmentVariables;
        readonly string templateDirectory;
        readonly string logPath;
        Dictionary<string, string> defaultEnvironmentVariables;
        readonly Version version;

        readonly VersionRange supportedVersionRange = new VersionRange(NuGetVersion.Parse("0.11.8"), true, NuGetVersion.Parse("0.15"), true);

        public TerraformCliExecutor(
            ILog log,
            ICalamariFileSystem fileSystem,
            ICommandLineRunner commandLineRunner,
            RunningDeployment deployment,
            Dictionary<string, string> environmentVariables
        )
        {
            this.log = log;
            this.fileSystem = fileSystem;
            this.commandLineRunner = commandLineRunner;
            this.deployment = deployment;
            variables = deployment.Variables;
            this.environmentVariables = environmentVariables;
            logPath = Path.Combine(deployment.CurrentDirectory, "terraform.log");

            templateDirectory = variables.Get(TerraformSpecialVariables.Action.Terraform.TemplateDirectory, deployment.CurrentDirectory);

            if (!string.IsNullOrEmpty(templateDirectory))
            {
                var templateDirectoryTemp = Path.Combine(deployment.CurrentDirectory, templateDirectory);

                if (!Directory.Exists(templateDirectoryTemp))
                    throw new Exception($"Directory {templateDirectory} does not exist.");

                templateDirectory = templateDirectoryTemp;
            }

            InitializeTerraformEnvironmentVariables();

            this.version = GetVersion();

            InitializePlugins();

            InitializeWorkspace();
        }

        public string ActionParams => variables.Get(TerraformSpecialVariables.Action.Terraform.AdditionalActionParams);

        /// <summary>
        /// Create a list of -var-file arguments from the newline separated list of variable files
        /// </summary>
        public string TerraformVariableFiles
        {
            get
            {
                var varFilesAsString = deployment.Variables.Get(TerraformSpecialVariables.Action.Terraform.VarFiles);

                if (varFilesAsString == null) return null;

                var varFiles = Regex.Split(varFilesAsString, "\r?\n")
                                    .Select(var => $"-var-file=\"{var}\"")
                                    .ToList();

                return string.Join(" ", varFiles);
            }
        }

        public CommandResult ExecuteCommand(params string[] arguments)
        {
            var commandResult = ExecuteCommandInternal(arguments, out var result, true);

            commandResult.VerifySuccess();
            return commandResult;
        }

        public CommandResult ExecuteCommand(out string result, params string[] arguments)
        {
            var commandResult = ExecuteCommandInternal(arguments, out result, true);

            return commandResult;
        }

        public CommandResult ExecuteCommand(out string result, bool outputToCalamariConsole, params string[] arguments)
        {
            var commandResult = ExecuteCommandInternal(arguments, out result, outputToCalamariConsole);

            return commandResult;
        }

        public void Dispose()
        {
            var attachLogFile = variables.GetFlag(TerraformSpecialVariables.Action.Terraform.AttachLogFile);
            if (attachLogFile)
            {
                var crashLogPath = Path.Combine(deployment.CurrentDirectory, "crash.log");

                if (fileSystem.FileExists(logPath))
                    log.NewOctopusArtifact(fileSystem.GetFullPath(logPath), fileSystem.GetFileName(logPath), fileSystem.GetFileSize(logPath));

                //When terraform crashes, the information would be contained in the crash.log file. We should attach this since
                //we don't want to blow that information away in case it provides something relevant https://www.terraform.io/docs/internals/debugging.html#interpreting-a-crash-log
                if (fileSystem.FileExists(crashLogPath))
                    log.NewOctopusArtifact(fileSystem.GetFullPath(crashLogPath), fileSystem.GetFileName(crashLogPath), fileSystem.GetFileSize(crashLogPath));
            }
        }

        CommandResult ExecuteCommandInternal(string[] arguments, out string result, bool outputToCalamariConsole)
        {
            var environmentVar = defaultEnvironmentVariables;
            if (environmentVariables != null)
                environmentVar.AddRange(environmentVariables);

            var terraformExecutable = variables.Get(TerraformSpecialVariables.Action.Terraform.CustomTerraformExecutable) ?? $"terraform{(CalamariEnvironment.IsRunningOnWindows ? ".exe" : string.Empty)}";
            var captureOutput = new CaptureInvocationOutputSink();
            var commandLineInvocation = new CommandLineInvocation(terraformExecutable, arguments)
            {
                WorkingDirectory = templateDirectory,
                EnvironmentVars = environmentVar,
                OutputToLog = outputToCalamariConsole,
                AdditionalInvocationOutputSink = captureOutput
            };

            log.Info(commandLineInvocation.ToString());

            var commandResult = commandLineRunner.Execute(commandLineInvocation);

            result = string.Join("\n", captureOutput.Infos);

            return commandResult;
        }

        void InitializePlugins()
        {
            var initParams = variables.Get(TerraformSpecialVariables.Action.Terraform.AdditionalInitParams);
            var allowPluginDownloads = variables.GetFlag(TerraformSpecialVariables.Action.Terraform.AllowPluginDownloads, true);
            string initCommand = $"init -no-color";
            
            if (version.IsLessThan("0.15.0"))
                initCommand += $" -get-plugins={allowPluginDownloads.ToString().ToLower()}";

            initCommand += $" {initParams}";

            ExecuteCommandInternal(
                                   new[] { initCommand },
                                   out _,
                                   true)
                .VerifySuccess();
        }

        Version GetVersion()
        {
            ExecuteCommandInternal(new[] { "--version" }, out string consoleOutput, true)
                .VerifySuccess();

            consoleOutput = consoleOutput.Replace("Terraform v", "");
            int newLinePos = consoleOutput.IndexOf('\n'); // this is always \n in all OSes for unknown reasons
            if (newLinePos != -1)
                consoleOutput = consoleOutput.Substring(0, newLinePos);

            if (!Version.TryParse(consoleOutput, out var version))
            {
                log.Warn($"Could not determine Terraform CLI version.");
            }
            else
            {
                if (!supportedVersionRange.Satisfies(new NuGetVersion(version)))
                    log.Warn($"Version {consoleOutput} of Terraform CLI is not supported. Supported version range is: {supportedVersionRange}");
            }

            return version;
        }

        void InitializeWorkspace()
        {
            var workspace = variables.Get(TerraformSpecialVariables.Action.Terraform.Workspace);

            if (!string.IsNullOrWhiteSpace(workspace))
            {
                ExecuteCommandInternal(new[] { "workspace list" }, out var results, true).VerifySuccess();

                foreach (var line in results.Split('\n'))
                {
                    var workspaceName = line.Trim('*', ' ');
                    if (workspaceName.Equals(workspace))
                    {
                        ExecuteCommandInternal(new[] { $"workspace select \"{workspace}\"" }, out _, true).VerifySuccess();
                        return;
                    }
                }

                ExecuteCommandInternal(new[] { $"workspace new \"{workspace}\"" }, out _, true).VerifySuccess();
            }
        }

        void InitializeTerraformEnvironmentVariables()
        {
            defaultEnvironmentVariables = ProxyEnvironmentVariablesGenerator.GenerateProxyEnvironmentVariables().ToDictionary(e => e.Key, e => e.Value);

            defaultEnvironmentVariables.Add("TF_IN_AUTOMATION", "1");
            defaultEnvironmentVariables.Add("TF_LOG", "TRACE");
            defaultEnvironmentVariables.Add("TF_LOG_PATH", logPath);
            defaultEnvironmentVariables.Add("TF_INPUT", "0");

            var customPluginDir = deployment.Variables.Get(TerraformSpecialVariables.Action.Terraform.PluginsDirectory);
            var pluginsPath = Path.Combine(deployment.CurrentDirectory, "terraformplugins");

            fileSystem.EnsureDirectoryExists(pluginsPath);

            if (!string.IsNullOrEmpty(customPluginDir))
                fileSystem.CopyDirectory(customPluginDir, pluginsPath);

            defaultEnvironmentVariables.Add("TF_PLUGIN_CACHE_DIR", pluginsPath);
        }

        class CaptureInvocationOutputSink : ICommandInvocationOutputSink
        {
            public List<string> Infos { get; } = new List<string>();

            public void WriteInfo(string line)
            {
                Infos.Add(line);
            }

            public void WriteError(string line)
            {
            }
        }
    }
}