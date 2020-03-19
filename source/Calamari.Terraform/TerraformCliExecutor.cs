using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Calamari.Deployment;
using Calamari.Extensions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Proxies;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Terraform
{
    class TerraformCliExecutor : IDisposable
    {
        readonly ILog log;
        readonly ICalamariFileSystem fileSystem;
        readonly RunningDeployment deployment;
        readonly IVariables variables;
        readonly Dictionary<string, string> environmentVariables;
        Dictionary<string, string> defaultEnvironmentVariables;
        readonly string templateDirectory;
        readonly string logPath;

        public TerraformCliExecutor(
            ILog log,
            ICalamariFileSystem fileSystem, 
            RunningDeployment deployment,
            Dictionary<string, string> environmentVariables
            )
        {
            this.log = log;
            this.fileSystem = fileSystem;
            this.deployment = deployment;
            this.variables = deployment.Variables;
            this.environmentVariables = environmentVariables;
            this.logPath = Path.Combine(deployment.CurrentDirectory, "terraform.log");

            templateDirectory = variables.Get(TerraformSpecialVariables.Action.Terraform.TemplateDirectory, deployment.CurrentDirectory);

            if (!String.IsNullOrEmpty(templateDirectory))
            {
                var templateDirectoryTemp = Path.Combine(deployment.CurrentDirectory, templateDirectory);

                if (!Directory.Exists(templateDirectoryTemp))
                {
                    throw new Exception($"Directory {templateDirectory} does not exist.");
                }

                templateDirectory = templateDirectoryTemp;
            }

            InitializeTerraformEnvironmentVariables();

            LogVersion();

            InitializePlugins();

            InitializeWorkspace();
        }

        public string ActionParams => variables.Get(TerraformSpecialVariables.Action.Terraform.AdditionalActionParams);

        public CommandResult ExecuteCommand(params string[] arguments)
        {
            var commandResult = ExecuteCommandInternal(ToSpaceSeparated(arguments), out _);

            commandResult.VerifySuccess();
            return commandResult;
        }

        public CommandResult ExecuteCommand(out string result, params string[] arguments)
        {
            var commandResult = ExecuteCommandInternal(ToSpaceSeparated(arguments), out result);

            return commandResult;
        }

        public CommandResult ExecuteCommand(out string result, ICommandOutput output, params string[] arguments)
        {
            var commandResult = ExecuteCommandInternal(ToSpaceSeparated(arguments), out result, output);

            return commandResult;
        }

        public void Dispose()
        {
            var attachLogFile = variables.GetFlag(TerraformSpecialVariables.Action.Terraform.AttachLogFile);
            if (attachLogFile)
            {
                
                var crashLogPath = Path.Combine(deployment.CurrentDirectory, "crash.log");

                if (fileSystem.FileExists(logPath))
                {
                    log.NewOctopusArtifact(fileSystem.GetFullPath(logPath), fileSystem.GetFileName(logPath), fileSystem.GetFileSize(logPath));
                }

                //When terraform crashes, the information would be contained in the crash.log file. We should attach this since
                //we don't want to blow that information away in case it provides something relevant https://www.terraform.io/docs/internals/debugging.html#interpreting-a-crash-log
                if (fileSystem.FileExists(crashLogPath))
                {
                    log.NewOctopusArtifact(fileSystem.GetFullPath(crashLogPath), fileSystem.GetFileName(crashLogPath), fileSystem.GetFileSize(crashLogPath));
                }
            }
        }

        static string ToSpaceSeparated(IEnumerable<string> items)
        {
            return string.Join(" ", items.Where(_ => !String.IsNullOrEmpty(_)));
        }

        CommandResult ExecuteCommandInternal(string arguments, out string result, ICommandOutput output = null)
        {
            var environmentVar = defaultEnvironmentVariables;
            if (environmentVariables != null)
            {
                environmentVar.MergeDictionaries(environmentVariables);
            }

            var terraformExecutable = variables.Get(TerraformSpecialVariables.Action.Terraform.CustomTerraformExecutable) ??
                                      $"terraform{(CalamariEnvironment.IsRunningOnWindows ? ".exe" : String.Empty)}";

            var commandLineInvocation = new CommandLineInvocation(terraformExecutable,
                arguments, templateDirectory, environmentVar);

            var commandOutput = new CaptureOutput(output ?? new ConsoleCommandOutput(log));
            var cmd = new CommandLineRunner(commandOutput);

            log.Info(commandLineInvocation.ToString());

            var commandResult = cmd.Execute(commandLineInvocation);

            result = String.Join("\n", commandOutput.Infos);

            return commandResult;
        }

        void InitializePlugins()
        {
            var initParams = variables.Get(TerraformSpecialVariables.Action.Terraform.AdditionalInitParams);
            var allowPluginDownloads = variables.GetFlag(TerraformSpecialVariables.Action.Terraform.AllowPluginDownloads, true);

            ExecuteCommandInternal(
                $"init -no-color -get-plugins={allowPluginDownloads.ToString().ToLower()} {initParams}", out _).VerifySuccess();
        }

        void LogVersion()
        {
            ExecuteCommandInternal($"--version", out _)
                .VerifySuccess();
        }

        void InitializeWorkspace()
        {
            var workspace = variables.Get(TerraformSpecialVariables.Action.Terraform.Workspace);

            if (!String.IsNullOrWhiteSpace(workspace))
            {
                ExecuteCommandInternal("workspace list", out var results).VerifySuccess();

                foreach (var line in results.Split('\n'))
                {
                    var workspaceName = line.Trim('*', ' ');
                    if (workspaceName.Equals(workspace))
                    {
                        ExecuteCommandInternal($"workspace select \"{workspace}\"", out _).VerifySuccess();
                        return;
                    }
                }

                ExecuteCommandInternal($"workspace new \"{workspace}\"", out _).VerifySuccess();
            }
        }

        class CaptureOutput : ICommandOutput
        {
            readonly ICommandOutput decorated;

            public CaptureOutput(ICommandOutput decorated)
            {
                this.decorated = decorated;
            }

            public List<string> Infos { get; } = new List<string>();

            public void WriteInfo(string line)
            {
                Infos.Add(line);
                decorated.WriteInfo(line);
            }

            public void WriteError(string line)
            {
                decorated.WriteError(line);
            }
        }


        /// <summary>
        /// Create a list of -var-file arguments from the newline separated list of variable files 
        /// </summary>
        public string TerraformVariableFiles =>
            deployment.Variables
                .Get(TerraformSpecialVariables.Action.Terraform.VarFiles)
                ?.Map(var => Regex.Split(var, "\r?\n"))
                .Select(var => $"-var-file=\"{var}\"")
                .ToList()
                .Map(list => string.Join(" ", list));

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
            {
                fileSystem.CopyDirectory(customPluginDir, pluginsPath);
            }

            defaultEnvironmentVariables.Add("TF_PLUGIN_CACHE_DIR", pluginsPath);
        }
    }
}