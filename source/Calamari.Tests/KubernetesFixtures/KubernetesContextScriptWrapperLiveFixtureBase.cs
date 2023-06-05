#if NETCORE
using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Aws.Integration;
using Calamari.Aws.Kubernetes.Discovery;
using Calamari.Commands;
using Calamari.Common.Features.Discovery;
using Calamari.Common.Features.EmbeddedResources;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.ServiceMessages;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;
using Calamari.Kubernetes.Commands;
using Calamari.Testing.Helpers;
using Calamari.Tests.Fixtures.Integration.FileSystem;
using Calamari.Tests.Helpers;
using FluentAssertions;
using Newtonsoft.Json;
using NUnit.Framework;
using KubernetesSpecialVariables = Calamari.Kubernetes.SpecialVariables;
using SpecialVariables = Calamari.Deployment.SpecialVariables;

namespace Calamari.Tests.KubernetesFixtures
{
    public abstract class KubernetesContextScriptWrapperLiveFixtureBase : CalamariFixture
    {
        protected const string TestNamespace = "calamari-testing";

        protected IVariables variables;
        protected string testFolder;

        [OneTimeSetUp]
        public void SetupTests()
        {
            testFolder = Path.GetDirectoryName(GetType().Assembly.FullLocalPath());
        }

        [SetUp]
        public void Setup()
        {
            variables = new CalamariVariables();

            Log = new DoNotDoubleLog();

            SetTestClusterVariables();
        }

        void SetTestClusterVariables()
        {

            variables.Set(KubernetesSpecialVariables.Namespace, TestNamespace);
            variables.Set(ScriptVariables.Syntax, CalamariEnvironment.IsRunningOnWindows ? ScriptSyntax.PowerShell.ToString() : ScriptSyntax.Bash.ToString());
        }

        CalamariResult ExecuteScript(IScriptWrapper wrapper, string scriptName) =>
            ExecuteScript(new[] { wrapper }, scriptName);

        CalamariResult ExecuteScript(IReadOnlyList<IScriptWrapper> additionalWrappers, string scriptName)
        {
            var wrappers = new List<IScriptWrapper>(additionalWrappers);
            if (variables.Get(SpecialVariables.Account.AccountType) == "AmazonWebServicesAccount")
            {
                wrappers.Add(new AwsScriptWrapper(Log, variables));
            }

            var result = ExecuteDirectlyWithScriptEngine(wrappers, scriptName);

            WriteLogMessagesToTestOutput();

            return result;
        }

        CalamariResult ExecuteDirectlyWithScriptEngine(IReadOnlyList<IScriptWrapper> wrappers, string scriptName)
        {
            var commandLineRunner = new CommandLineRunner(Log, variables);
            var engine = new ScriptEngine(wrappers);
            var result = engine.Execute(new Script(scriptName), variables, commandLineRunner, GetEnvironments());

            return new CalamariResult(result.ExitCode, new CaptureCommandInvocationOutputSink());
        }

        protected void DeployWithDeploymentScriptAndVerifyResult(Func<string, string> addFilesOrPackageFunc = null, bool shouldSucceed = true)
        {
            DeployWithScriptAndVerifyResult(() =>
            {
                var scriptPath = Path.Combine(testFolder, "KubernetesFixtures/Scripts");
                var bashScript = File.ReadAllText(Path.Combine(scriptPath, "KubernetesDeployment.sh"));
                var powershellScript = File.ReadAllText(Path.Combine(scriptPath, "KubernetesDeployment.ps1"));
                return (bashScript, powershellScript);
            }, addFilesOrPackageFunc, shouldSucceed);
        }

        protected void DeployWithKubectlTestScriptAndVerifyResult()
        {
            DeployWithScriptAndVerifyResult("#{Octopus.Action.Kubernetes.CustomKubectlExecutable} cluster-info");
        }

        protected void DeployWithNonKubectlTestScriptAndVerifyResult()
        {
            DeployWithScriptAndVerifyResult("echo running target script...");
        }

        private void DeployWithScriptAndVerifyResult(string script,
            Func<string, string> addFilesOrPackageFunc = null, bool shouldSucceed = true)
        {
            DeployWithScriptAndVerifyResult(() => (script, script), addFilesOrPackageFunc, shouldSucceed);
        }

        private void DeployWithScriptAndVerifyResult(Func<(string bashScript, string powershellScript)> scriptFactory,
            Func<string, string> addFilesOrPackageFunc = null, bool shouldSucceed = true)
        {
            SetupTempDirectoryAndVerifyResult(addFilesOrPackageFunc, (d, p) =>
            {
                var (bashScript, powershellScript) = scriptFactory();
                variables.Set(SpecialVariables.Action.Script.ScriptBodyBySyntax(ScriptSyntax.Bash),
                    bashScript);
                variables.Set(SpecialVariables.Action.Script.ScriptBodyBySyntax(ScriptSyntax.PowerShell),
                    powershellScript);

                return ExecuteCommand(RunScriptCommand.Name, d, p);
            }, shouldSucceed);
        }

        protected void ExecuteCommandAndVerifyResult(string commandName,
            Func<string, string> addFilesOrPackageFunc = null,
            bool shouldSucceed = true)
        {
            SetupTempDirectoryAndVerifyResult(addFilesOrPackageFunc, (d, p) => ExecuteCommand(commandName, d, p), shouldSucceed);
        }

        private void SetupTempDirectoryAndVerifyResult(Func<string, string> addFilesOrPackageFunc, Func<string, string, CalamariResult> func, bool shouldSucceed)
        {
            using (var dir = TemporaryDirectory.Create())
            {
                var directoryPath = dir.DirectoryPath;
                var folderPath = Path.Combine(directoryPath, "TestFolder");
                Directory.CreateDirectory(folderPath);

                var packagePath = addFilesOrPackageFunc?.Invoke(directoryPath);

                var output = func(directoryPath, packagePath);

                WriteLogMessagesToTestOutput();

                if (shouldSucceed)
                {
                    output.AssertSuccess();
                }
                else
                {
                    output.AssertFailure();
                }
            }
        }

        CalamariResult ExecuteCommand(string command, string workingDirectory, string packagePath)
        {
            using (var variablesFile = new TemporaryFile(Path.GetTempFileName()))
            {
                variables.Save(variablesFile.FilePath);

                var calamariCommand = Calamari().Action(command)
                                                .Argument("variables", variablesFile.FilePath)
                                                .WithEnvironmentVariables(GetEnvironments())
                                                .WithWorkingDirectory(workingDirectory)
                                                .OutputToLog(true);

                if (packagePath != null)
                {
                    calamariCommand.Argument("package", packagePath);
                }

                return Invoke(calamariCommand, variables, Log);
            }
        }

        protected virtual Dictionary<string, string> GetEnvironments()
        {
            return new Dictionary<string, string>();
        }

        protected void DoDiscovery(AwsAuthenticationDetails authenticationDetails)
        {
            var scope = new TargetDiscoveryScope("TestSpace",
                "Staging",
                "testProject",
                null,
                new[] { "discovery-role" },
                "WorkerPools-1",
                null);

            var targetDiscoveryContext =
                new TargetDiscoveryContext<AwsAuthenticationDetails>(scope,
                    authenticationDetails);

            ExecuteDiscoveryCommandAndVerifyResult(targetDiscoveryContext);
        }

        protected void DoDiscoveryAndAssertReceivedServiceMessageWithMatchingProperties(
            AwsAuthenticationDetails authenticationDetails,
            Dictionary<string,string> properties)
        {
            DoDiscovery(authenticationDetails);

            var expectedServiceMessage = new ServiceMessage(
                KubernetesDiscoveryCommand.CreateKubernetesTargetServiceMessageName,
                properties);

            Log.ServiceMessages.Should()
                .ContainSingle(s => s.Properties["name"] == properties["name"])
                .Which.Should()
                .BeEquivalentTo(expectedServiceMessage);
        }

        protected void ExecuteDiscoveryCommandAndVerifyResult<TAuthenticationDetails>(
            TargetDiscoveryContext<TAuthenticationDetails> discoveryContext)
            where TAuthenticationDetails : class, ITargetDiscoveryAuthenticationDetails
        {
            variables.Add(KubernetesDiscoveryCommand.ContextVariableName,
                JsonConvert.SerializeObject(discoveryContext));

            SetupTempDirectoryAndVerifyResult(null,
                (dir, _) => ExecuteCommand(KubernetesDiscoveryCommand.Name, dir, null), shouldSucceed: true);
        }

        private void WriteLogMessagesToTestOutput()
        {
            foreach (var message in Log.Messages)
            {
                Console.WriteLine($"[{message.Level}] {message.FormattedMessage}");
            }
        }
    }
}
#endif