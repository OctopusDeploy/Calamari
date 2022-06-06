#if NETCORE
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Extensions;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Tests.KubernetesFixtures
{
    public abstract class KubernetesContextScriptWrapperLiveFixture: KubernetesContextScriptWrapperLiveFixtureBase
    {
        protected const string KubeCtlExecutableVariableName = "Octopus.Action.Kubernetes.CustomKubectlExecutable";
        protected const string KubeConfigFileName = "kubeconfig.tpl";
        
        InstallTools installTools;

        string terraformWorkingFolder;
        
        protected abstract string KubernetesCloudProvider { get; }

        protected virtual Task PreInitialise() { return Task.CompletedTask; }
        
        protected virtual Task InstallOptionalTools(InstallTools tools) { return Task.CompletedTask; }

        [OneTimeSetUp]
        public async Task SetupInfrastructure()
        {
            await PreInitialise();
            
            terraformWorkingFolder = InitialiseTerraformWorkingFolder($"terraform_working/{KubernetesCloudProvider}", 
                $"KubernetesFixtures/Terraform/Clusters/{KubernetesCloudProvider}");
        
            installTools = new InstallTools(TestContext.Progress.WriteLine);
            await installTools.Install();
            await InstallOptionalTools(installTools);
        
            InitialiseInfrastructure(terraformWorkingFolder);
        }

        [OneTimeTearDown]
        public void TearDownInfrastructure()
        {
            RunTerraformDestroy(terraformWorkingFolder);
        }

        [SetUp]
        public void SetExtraVariables()
        {
            variables.Set(KubeCtlExecutableVariableName, installTools.KubectlExecutable);
        }

        protected override Dictionary<string, string> GetEnvironments()
        {
            var currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            var delimiter = CalamariEnvironment.IsRunningOnWindows ? ";" : ":";
            
            var toolsToAdd = ToolsToAddToPath(installTools).ToList();

            if (!toolsToAdd.IsNullOrEmpty())
            {
                foreach (var tool in toolsToAdd)
                {
                    if (currentPath.Length > 0 && !currentPath.EndsWith(delimiter))
                    {
                        currentPath += delimiter;
                    }
                    currentPath += Path.GetDirectoryName(tool);
                }
            }

            return new Dictionary<string, string> { { "PATH", currentPath } };
        }

        protected abstract IEnumerable<string> ToolsToAddToPath(InstallTools tools);

        protected abstract void ExtractVariablesFromTerraformOutput(JObject jsonOutput);

        void InitialiseInfrastructure(string terraformWorkingFolder)
        {
            RunTerraformInternal(terraformWorkingFolder, "init");
            RunTerraformInternal(terraformWorkingFolder, "apply", "-auto-approve");
            var jsonOutput = JObject.Parse(RunTerraformOutput(terraformWorkingFolder));
        
            ExtractVariablesFromTerraformOutput(jsonOutput);
        }

        protected void RunTerraformDestroy(string terraformWorkingFolder, Dictionary<string, string> env = null)
        {
            RunTerraformInternal(terraformWorkingFolder, env ?? new Dictionary<string, string>(), "destroy", "-auto-approve");
        }
        
        string RunTerraformOutput(string terraformWorkingFolder)
        {
            return RunTerraformInternal(terraformWorkingFolder, new Dictionary<string, string>(), false, "output", "-json");
        }
        
        string RunTerraformInternal(string terraformWorkingFolder, params string[] args)
        {
            return RunTerraformInternal(terraformWorkingFolder, new Dictionary<string, string>(), args);
        }
        
        protected string RunTerraformInternal(string terraformWorkingFolder, Dictionary<string, string> env, params string[] args)
        {
            return RunTerraformInternal(terraformWorkingFolder, env, true, args);
        }

        protected abstract Dictionary<string, string> GetEnvironmentVars();

        string RunTerraformInternal(string terraformWorkingFolder, Dictionary<string, string> env, bool printOut, params string[] args)
        {
            var sb = new StringBuilder();
            var environmentVars = GetEnvironmentVars();
            environmentVars["TF_IN_AUTOMATION"] = bool.TrueString;
            environmentVars.AddRange(env);

            var result = SilentProcessRunner.ExecuteCommand(installTools.TerraformExecutable,
                string.Join(" ", args.Concat(new[] { "-no-color" })),
                terraformWorkingFolder,
                environmentVars,
                s =>
                {
                    sb.AppendLine(s);
                    if (printOut)
                    {
                        TestContext.Progress.WriteLine(s);
                    }
                },
                e =>
                {
                    TestContext.Error.WriteLine(e);
                });
        
            result.ExitCode.Should().Be(0);
        
            return sb.ToString().Trim(Environment.NewLine.ToCharArray());
        }

        protected string InitialiseTerraformWorkingFolder(string folderName, string filesSource)
        {
            var workingFolder = Path.Combine(testFolder, folderName);
            if (Directory.Exists(workingFolder))
                Directory.Delete(workingFolder, true);
            
            Directory.CreateDirectory(workingFolder);

            // If required by the terraform script,
            // the kube config file will be in the parent folder.
            var kubeConfigFile = Path.Join(Directory.GetParent(filesSource).FullName, KubeConfigFileName);
            if (File.Exists(kubeConfigFile))
            {
                var destination = Path.Combine(workingFolder, KubeConfigFileName);
                Console.WriteLine($"Copying {kubeConfigFile} to {destination}");
                File.Copy(kubeConfigFile, destination, overwrite: true);
            }
            else
            {
                Console.WriteLine($"{kubeConfigFile} not found, so not copying.");
            }

            foreach (var file in Directory.EnumerateFiles(Path.Combine(testFolder, filesSource)))
            {
                File.Copy(file, Path.Combine(workingFolder, Path.GetFileName(file)), overwrite: true);
            }

            return workingFolder;
        }
    }
}
#endif