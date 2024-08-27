#if NETCORE
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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
        static readonly CancellationTokenSource CancellationTokenSource = new CancellationTokenSource();
        readonly CancellationToken cancellationToken = CancellationTokenSource.Token;
        
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
        
            await InitialiseInfrastructure(terraformWorkingFolder);
        }

        [OneTimeTearDown]
        public async Task TearDownInfrastructure()
        {
            await RunTerraformDestroy(terraformWorkingFolder);
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

        async Task InitialiseInfrastructure(string terraformWorkingFolder)
        {
            await RunTerraformInternal(terraformWorkingFolder, "init");
            await RunTerraformInternal(terraformWorkingFolder, "apply", "-auto-approve");
            var jsonOutput = JObject.Parse(await RunTerraformOutput(terraformWorkingFolder));
        
            ExtractVariablesFromTerraformOutput(jsonOutput);
        }

        protected async Task RunTerraformDestroy(string terraformWorkingFolder, Dictionary<string, string> env = null)
        {
            await RunTerraformInternal(terraformWorkingFolder, env ?? new Dictionary<string, string>(), "destroy", "-auto-approve");
        }
        
        async Task<string> RunTerraformOutput(string terraformWorkingFolder)
        {
            return await RunTerraformInternal(terraformWorkingFolder, new Dictionary<string, string>(), false, "output", "-json");
        }
        
        async Task<string> RunTerraformInternal(string terraformWorkingFolder, params string[] args)
        {
            return await RunTerraformInternal(terraformWorkingFolder, new Dictionary<string, string>(), args);
        }
        
        protected async Task<string> RunTerraformInternal(string terraformWorkingFolder, Dictionary<string, string> env, params string[] args)
        {
            return await RunTerraformInternal(terraformWorkingFolder, env, true, args);
        }

        protected abstract Task<Dictionary<string, string>> GetEnvironmentVars(CancellationToken cancellationToken);

        async Task<string> RunTerraformInternal(string terraformWorkingFolder, Dictionary<string, string> env, bool printOut, params string[] args)
        {
            var stdOut = new StringBuilder();
            var environmentVars = await GetEnvironmentVars(cancellationToken);
            environmentVars["TF_IN_AUTOMATION"] = bool.TrueString;
            environmentVars.AddRange(env);

            var result = SilentProcessRunner.ExecuteCommand(installTools.TerraformExecutable,
                string.Join(" ", args.Concat(new[] { "-no-color" })),
                terraformWorkingFolder,
                environmentVars,
                s =>
                {
                    stdOut.AppendLine(s);
                    if (printOut)
                    {
                        TestContext.Progress.WriteLine(s);
                    }
                },
                e =>
                {
                    TestContext.Error.WriteLine(e);
                });
        
            result.ExitCode.Should().Be(0, because: $"`terraform {args[0]}` should run without error and exit cleanly during infrastructure setup. Error output: \\r\\n{{result.ErrorOutput}}\");");
        
            return stdOut.ToString().Trim(Environment.NewLine.ToCharArray());
        }

        protected string InitialiseTerraformWorkingFolder(string folderName, string filesSource)
        {
            var workingFolder = Path.Combine(testFolder, folderName);
            if (Directory.Exists(workingFolder))
                Directory.Delete(workingFolder, true);
            
            Directory.CreateDirectory(workingFolder);
            foreach (var file in Directory.EnumerateFiles(Path.Combine(testFolder, filesSource)))
            {
                File.Copy(file, Path.Combine(workingFolder, Path.GetFileName(file)), overwrite: true);
            }

            return workingFolder;
        }
    }
}
#endif