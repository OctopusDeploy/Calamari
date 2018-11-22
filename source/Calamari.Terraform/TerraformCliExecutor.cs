using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Calamari.Deployment;
using Calamari.Hooks;
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
        private StringDictionary defaultEnvironmentVariables;
        private readonly string logPath;

        public TerraformCLIExecutor(ICalamariFileSystem fileSystem, RunningDeployment deployment)
        {
            this.fileSystem = fileSystem;
            this.deployment = deployment;

            var variables = deployment.Variables;
            TemplateDirectory = variables.Get(TerraformSpecialVariables.Action.Terraform.TemplateDirectory, deployment.CurrentDirectory);
            Workspace = variables.Get(TerraformSpecialVariables.Action.Terraform.Workspace);
            InitParams = variables.Get(TerraformSpecialVariables.Action.Terraform.AdditionalInitParams);
            ActionParams = variables.Get(TerraformSpecialVariables.Action.Terraform.AdditionalActionParams);
            TerraformExecutable = variables.Get(TerraformSpecialVariables.Action.Terraform.CustomTerraformExecutable)
                .Map(path => String.IsNullOrWhiteSpace(path) ? Path.Combine(deployment.CurrentDirectory, "terraform.exe") : path);
            
            AllowPluginDownloads = variables.GetFlag(TerraformSpecialVariables.Action.Terraform.AllowPluginDownloads, true);
            AttachLogFile = variables.GetFlag(TerraformSpecialVariables.Action.Terraform.AttachLogFile);
            TerraformVariableFiles = GenerateVarFiles();

            logPath = Path.Combine(deployment.CurrentDirectory, "terraform.log");

            if (!String.IsNullOrEmpty(TemplateDirectory))
            {
                var templateDirectoryTemp = Path.Combine(deployment.CurrentDirectory, TemplateDirectory);

                if (!Directory.Exists(templateDirectoryTemp))
                {
                    throw new Exception($"Directory {TemplateDirectory} does not exist.");
                }

                TemplateDirectory = templateDirectoryTemp;
            }

            InitialiseTerraformEnvironmentVariables();

            InitialisePlugins();

            InitialiseWorkspace();
        }

        public string ExecuteCommand(string arguments, StringDictionary environmentVariables)
        {
            var commandResult = ExecuteCommandInternal(arguments, out var result, environmentVariables);

            commandResult.VerifySuccess();

            return result;
        }

        public int ExecuteCommand(string arguments, StringDictionary environmentVariables, out string result)
        {
            var commandResult = ExecuteCommandInternal(arguments, out result, environmentVariables);

            return commandResult.ExitCode;
        }

        public void Dispose()
        {
            if (AttachLogFile)
            {
                if (fileSystem.FileExists(logPath))
                {
                    Log.NewOctopusArtifact(fileSystem.GetFullPath(logPath), fileSystem.GetFileName(logPath), fileSystem.GetFileSize(logPath));
                }
            }
        }

        CommandResult ExecuteCommandInternal(string arguments, out string result, StringDictionary environmentVariables = null)
        {
            var commandLineInvocation = new CommandLineInvocation(TerraformExecutable,
                arguments, TemplateDirectory, environmentVariables == null 
                    ? defaultEnvironmentVariables
                    : defaultEnvironmentVariables.MergeDictionaries(environmentVariables));

            var commandOutput = new CaptureOutput();
            var cmd = new CommandLineRunner(commandOutput);
            var commandResult = cmd.Execute(commandLineInvocation);

            Log.Info(commandLineInvocation.ToString());

            result = String.Join("\n", commandOutput.Infos);

            return commandResult;
        }

        void InitialisePlugins()
        {
            ExecuteCommandInternal(
                $"init -no-color -get-plugins={AllowPluginDownloads.ToString().ToLower()} {InitParams}", out _).VerifySuccess();
        }

        void InitialiseWorkspace()
        {
            if (!String.IsNullOrWhiteSpace(Workspace))
            {
                ExecuteCommandInternal("workspace list", out var results).VerifySuccess();
                
                foreach (var line in results.Split('\n'))
                {
                    var workspaceName = line.Trim('*', ' ');
                    if (workspaceName.Equals(Workspace))
                    {
                        ExecuteCommandInternal($"workspace select {Workspace}", out _).VerifySuccess();
                        return;
                    }
                }

                ExecuteCommandInternal($"workspace new {Workspace}", out _).VerifySuccess();
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

        void InitialiseTerraformEnvironmentVariables()
        {
            defaultEnvironmentVariables = new CommandLineToolsProxyEnvironmentVariables().EnvironmentVariables;

            defaultEnvironmentVariables.Add("TF_IN_AUTOMATION", "1");
            defaultEnvironmentVariables.Add("TF_LOG", "TRACE");
            defaultEnvironmentVariables.Add("TF_LOG_PATH", logPath);
            defaultEnvironmentVariables.Add("TF_INPUT", "0");

            var pluginDir = deployment.Variables.Get("Octopus.Action.Terraform.PluginsDirectory");
            if(string.IsNullOrEmpty(pluginDir))
            {
                var cliPath = deployment.Variables.Get("Octopus.Calamari.TerraformCliPath");
                pluginDir = Path.Combine(cliPath, "contentFiles\\any\\win\\plugins");
            }

            defaultEnvironmentVariables.Add("TF_PLUGIN_CACHE_DIR", pluginDir);
        }
    }
}
