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
        private readonly RunningDeployment deployment;
        readonly Dictionary<string, string> environmentVariables;
        private Dictionary<string, string> defaultEnvironmentVariables;
        private readonly string logPath;
        readonly string crashLogPath;
        
        public TerraformCLIExecutor(ICalamariFileSystem fileSystem, RunningDeployment deployment,
            Dictionary<string, string> environmentVariables)
        {
            this.fileSystem = fileSystem;
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

            InitializePlugins();

            InitializeWorkspace();
        }

        public string ExecuteCommand(params string[] arguments)
        {
            var commandResult = ExecuteCommandInternal(ToSpaceSeparated(arguments), out var result);

            commandResult.VerifySuccess();

            return result;
        }

        public CommandResult ExecuteCommand(out string result, params string[] arguments)
        {
            var commandResult = ExecuteCommandInternal(ToSpaceSeparated(arguments), out result);

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
                
                if (fileSystem.FileExists(crashLogPath))
                {
                    Log.NewOctopusArtifact(fileSystem.GetFullPath(crashLogPath), fileSystem.GetFileName(crashLogPath), fileSystem.GetFileSize(crashLogPath));
                }
            }
        }

        static string ToSpaceSeparated(IEnumerable<string> items)
        {
            return string.Join(" ", items.Where(_ => !String.IsNullOrEmpty(_)));
        }

        CommandResult ExecuteCommandInternal(string arguments, out string result)
        {
            var environmentVar = defaultEnvironmentVariables;
            if (environmentVariables != null)
            {
                environmentVar.MergeDictionaries(environmentVariables);
            }

            var commandLineInvocation = new CommandLineInvocation(TerraformExecutable,
                arguments, TemplateDirectory, environmentVar);

            var commandOutput = new CaptureOutput();
            var cmd = new CommandLineRunner(commandOutput);
            
            Log.Info(commandLineInvocation.ToString());
            
            var commandResult = cmd.Execute(commandLineInvocation);
            
            result = String.Join("\n", commandOutput.Infos);

            return commandResult;
        }

        void InitializePlugins()
        {
            ExecuteCommandInternal(
                $"init -no-color -get-plugins={AllowPluginDownloads.ToString().ToLower()} {InitParams}", out _).VerifySuccess();
        }

        void InitializeWorkspace()
        {
            if (!String.IsNullOrWhiteSpace(Workspace))
            {
                ExecuteCommandInternal("workspace list", out var results).VerifySuccess();
                
                foreach (var line in results.Split('\n'))
                {
                    var workspaceName = line.Trim('*', ' ');
                    if (workspaceName.Equals(Workspace))
                    {
                        ExecuteCommandInternal($"workspace select \"{Workspace}\"", out _).VerifySuccess();
                        return;
                    }
                }

                ExecuteCommandInternal($"workspace new \"{Workspace}\"", out _).VerifySuccess();
            }
        }

        class CaptureOutput : ICommandOutput
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
            defaultEnvironmentVariables = new CommandLineToolsProxyEnvironmentVariables().EnvironmentVariables;

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
