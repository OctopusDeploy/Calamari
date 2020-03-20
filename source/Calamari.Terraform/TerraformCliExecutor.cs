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
    class TerraformCLIExecutor : IDisposable
    {
        private readonly ICalamariFileSystem fileSystem;
        readonly ICommandLineRunner commandLineRunner;
        private readonly RunningDeployment deployment;
        readonly Dictionary<string, string> environmentVariables;
        private Dictionary<string, string> defaultEnvironmentVariables;
        private readonly string logPath;
        readonly string crashLogPath;

        public TerraformCLIExecutor(
            ICalamariFileSystem fileSystem,
            ICommandLineRunner commandLineRunner,
            RunningDeployment deployment,
            Dictionary<string, string> environmentVariables
        )
        {
            this.fileSystem = fileSystem;
            this.commandLineRunner = commandLineRunner;
            this.deployment = deployment;
            this.environmentVariables = environmentVariables;

            var variables = deployment.Variables;
            TemplateDirectory = variables.Get(TerraformSpecialVariables.Action.Terraform.TemplateDirectory, deployment.CurrentDirectory);
            Workspace = variables.Get(TerraformSpecialVariables.Action.Terraform.Workspace);
            InitParams = variables.Get(TerraformSpecialVariables.Action.Terraform.AdditionalInitParams);
            ActionParams = variables.Get(TerraformSpecialVariables.Action.Terraform.AdditionalActionParams);
            TerraformExecutable = variables.Get(TerraformSpecialVariables.Action.Terraform.CustomTerraformExecutable) ??
                                  $"terraform{(CalamariEnvironment.IsRunningOnWindows ? ".exe" : String.Empty)}";
            AllowPluginDownloads = variables.GetFlag(TerraformSpecialVariables.Action.Terraform.AllowPluginDownloads, true);
            AttachLogFile = variables.GetFlag(TerraformSpecialVariables.Action.Terraform.AttachLogFile);
            TerraformVariableFiles = GenerateVarFiles();
            
            logPath = Path.Combine(deployment.CurrentDirectory, "terraform.log");
            crashLogPath = Path.Combine(deployment.CurrentDirectory, "crash.log");

            if (!String.IsNullOrEmpty(TemplateDirectory))
            {
                var templateDirectoryTemp = Path.Combine(deployment.CurrentDirectory, TemplateDirectory);

                if (!Directory.Exists(templateDirectoryTemp))
                {
                    throw new Exception($"Directory {TemplateDirectory} does not exist.");
                }

                TemplateDirectory = templateDirectoryTemp;
            }

            InitializeTerraformEnvironmentVariables();

            LogVersion();

            InitializePlugins();

            InitializeWorkspace();
        }

        public CommandResult ExecuteCommand(params string[] arguments)
        {
            var commandResult = ExecuteCommandInternal(arguments, out _, true);

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
            if (AttachLogFile)
            {
                if (fileSystem.FileExists(logPath))
                {
                    Log.NewOctopusArtifact(fileSystem.GetFullPath(logPath), fileSystem.GetFileName(logPath), fileSystem.GetFileSize(logPath));
                }
                
                //When terraform crashes, the information would be contained in the crash.log file. We should attach this since
                //we don't want to blow that information away in case it provides something relevant https://www.terraform.io/docs/internals/debugging.html#interpreting-a-crash-log
                if (fileSystem.FileExists(crashLogPath))
                {
                    Log.NewOctopusArtifact(fileSystem.GetFullPath(crashLogPath), fileSystem.GetFileName(crashLogPath), fileSystem.GetFileSize(crashLogPath));
                }
            }
        }

        CommandResult ExecuteCommandInternal(string[] arguments, out string result, bool outputToCalamariConsole)
        {
            var environmentVar = defaultEnvironmentVariables;
            if (environmentVariables != null)
            {
                environmentVar.MergeDictionaries(environmentVariables);
            }

            var captureOutput = new CaptureInvocationOutputSink();
            var commandLineInvocation = new CommandLineInvocation(TerraformExecutable, arguments)
            {
                WorkingDirectory = TemplateDirectory,
                EnvironmentVars = environmentVar,
                OutputToCalamariConsole = outputToCalamariConsole,
                AdditionalInvocationOutputSink = captureOutput
            };

            Log.Info(commandLineInvocation.ToString());
            
            var commandResult = commandLineRunner.Execute(commandLineInvocation);
            
            result = String.Join("\n", captureOutput.Infos);

            return commandResult;
        }

        void InitializePlugins()
        {
            ExecuteCommandInternal(
                new[] {$"init -no-color -get-plugins={AllowPluginDownloads.ToString().ToLower()} {InitParams}"}, out _, true).VerifySuccess();
        }
        
        void LogVersion()
        {
            ExecuteCommandInternal(new[] {$"--version"}, out _, true)
                .VerifySuccess();
        }

        void InitializeWorkspace()
        {
            if (!String.IsNullOrWhiteSpace(Workspace))
            {
                ExecuteCommandInternal(new[] {"workspace list"}, out var results, true).VerifySuccess();
                
                foreach (var line in results.Split('\n'))
                {
                    var workspaceName = line.Trim('*', ' ');
                    if (workspaceName.Equals(Workspace))
                    {
                        ExecuteCommandInternal(new[] {$"workspace select \"{Workspace}\""}, out _, true).VerifySuccess();
                        return;
                    }
                }

                ExecuteCommandInternal(new[] {$"workspace new \"{Workspace}\""}, out _, true).VerifySuccess();
            }
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

        public string TemplateDirectory { get; }

        public string Workspace { get; }

        public string InitParams { get; }

        public string ActionParams { get; }

        public string TerraformExecutable { get; }

        public string TerraformVariableFiles { get; }

        public bool AllowPluginDownloads { get; }

        public bool AttachLogFile { get; }

        /// <summary>
        /// Create a list of -var-file arguments from the newline separated list of variable files 
        /// </summary>
        string GenerateVarFiles() => 
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

            if(!string.IsNullOrEmpty(customPluginDir))
            {
                fileSystem.CopyDirectory(customPluginDir, pluginsPath);
            }

            defaultEnvironmentVariables.Add("TF_PLUGIN_CACHE_DIR", pluginsPath);
        }
    }
}