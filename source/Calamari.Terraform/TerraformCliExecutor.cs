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
            TemplateDirectory = variables.Get(TerraformSpecialVariables.Action.Terraform.TemplateDirectory);
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
            var commandResult = ExecuteCommandInternal(arguments, environmentVariables, out var result);

            commandResult.VerifySuccess();

            return result;
        }

        public int ExecuteCommand(string arguments, StringDictionary environmentVariables, out string result)
        {
            var commandResult = ExecuteCommandInternal(arguments, environmentVariables, out result);

            return commandResult.ExitCode;
        }

        public CommandResult ExecuteCommandInternal(string arguments, StringDictionary environmentVariables, out string result)
        {
            var commandLineInvocation = new CommandLineInvocation(TerraformExecutable,
                arguments, TemplateDirectory, defaultEnvironmentVariables.MergeDictionaries(environmentVariables));

            var commandOutput = new CaptureOutput();
            var cmd = new CommandLineRunner(commandOutput);
            var commandResult = cmd.Execute(commandLineInvocation);

            result = String.Join(Environment.NewLine, commandOutput.Infos);

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
            }
        }

        void InitialisePlugins()
        {
            var commandLineInvocation = new CommandLineInvocation(TerraformExecutable,
                $"init -no-color -input=false -get-plugins={AllowPluginDownloads.ToString().ToLower()} {InitParams}"
                , TemplateDirectory, defaultEnvironmentVariables);

            var commandOutput = new CaptureOutput();
            var cmd = new CommandLineRunner(commandOutput);
            cmd.Execute(commandLineInvocation).VerifySuccess();
        }

        void InitialiseWorkspace()
        {
            if (!String.IsNullOrWhiteSpace(Workspace))
            {
                var commandLineInvocation = new CommandLineInvocation(TerraformExecutable,
                    "workspace list"
                    , TemplateDirectory, defaultEnvironmentVariables);
                var commandOutput =  new CaptureOutput();
                var cmd = new CommandLineRunner(commandOutput);
                cmd.Execute(commandLineInvocation).VerifySuccess();

                foreach (var line in commandOutput.Infos)
                {
                    var workspaceName = line.Trim('*', ' ');
                    if (workspaceName.Equals(Workspace))
                    {
                        commandLineInvocation = new CommandLineInvocation(TerraformExecutable,
                            $"workspace select {Workspace}"
                            , TemplateDirectory, defaultEnvironmentVariables);
                        cmd.Execute(commandLineInvocation).VerifySuccess();
                        return;
                    }
                }

                commandLineInvocation = new CommandLineInvocation(TerraformExecutable,
                    $"workspace new {Workspace}"
                    , TemplateDirectory, defaultEnvironmentVariables);
                cmd.Execute(commandLineInvocation).VerifySuccess();
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
                .Select(var => $"-var-file=\'{var}\'")
                .ToList()
                .Map(list => string.Join(" ", list));

        void InitialiseTerraformEnvironmentVariables()
        {
            defaultEnvironmentVariables = new CommandLineToolsProxyEnvironmentVariables().EnvironmentVariables;

            defaultEnvironmentVariables.Add("TF_IN_AUTOMATION", "1");
            defaultEnvironmentVariables.Add("TF_LOG", "TRACE");
            defaultEnvironmentVariables.Add("TF_LOG_PATH", logPath);

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
