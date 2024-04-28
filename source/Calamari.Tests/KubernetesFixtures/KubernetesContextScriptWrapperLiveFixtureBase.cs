#if NETCORE
using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Aws.Kubernetes.Discovery;
using Calamari.Commands;
using Calamari.Common.Features.Discovery;
using Calamari.Common.Features.Scripts;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.ServiceMessages;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.Commands;
using Calamari.Testing.Helpers;
using Calamari.Tests.Helpers;
using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
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
            variables.Set(KnownVariables.EnabledFeatureToggles, FeatureToggle.KubernetesAksKubeloginFeatureToggle.ToString());

            Log = new DoNotDoubleLog();

            SetTestClusterVariables();
        }

        protected void DeployWithKubectlTestScriptAndVerifyResult()
        {
            SetInlineScriptVariables("#{Octopus.Action.Kubernetes.CustomKubectlExecutable} cluster-info");
            ExecuteCommandAndVerifyResult(RunScriptCommand.Name);
        }

        protected void DeployWithNonKubectlTestScriptAndVerifyResult()
        {
            SetInlineScriptVariables("echo running target script...");
            ExecuteCommandAndVerifyResult(RunScriptCommand.Name);
        }

        protected void ExecuteCommandAndVerifyResult(string commandName, Func<string, string> addFilesOrPackageFunc = null, bool shouldSucceed = true)
        {
            using (var dir = TemporaryDirectory.Create())
            {
                var directoryPath = dir.DirectoryPath;
                // Note: the "Test Folder" has a space in it to test that working directories
                // with spaces are handled correctly by Kubernetes Steps.
                var folderPath = Path.Combine(directoryPath, "Test Folder");
                Directory.CreateDirectory(folderPath);

                var packagePath = addFilesOrPackageFunc?.Invoke(folderPath);

                var output = ExecuteCommand(commandName, folderPath, packagePath);

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

        protected virtual Dictionary<string, string> GetEnvironments()
        {
            return new Dictionary<string, string>();
        }

        protected void DoDiscovery<TCredentials>(AwsAuthenticationDetails<TCredentials> authenticationDetails)
            where TCredentials : AwsCredentialsBase
        {
            var scope = new TargetDiscoveryScope("TestSpace",
                "Staging",
                "testProject",
                null,
                new[] { "discovery-role" },
                "WorkerPools-1",
                null);

            var targetDiscoveryContext =
                new TargetDiscoveryContext<AwsAuthenticationDetails<TCredentials>>(scope,
                    authenticationDetails);

            ExecuteDiscoveryCommandAndVerifyResult(targetDiscoveryContext);
        }

        protected void DoDiscoveryAndAssertReceivedServiceMessageWithMatchingProperties<TCredentials>(
            AwsAuthenticationDetails<TCredentials> authenticationDetails,
            Dictionary<string,string> properties)
            where TCredentials : AwsCredentialsBase
        {
            DoDiscovery(authenticationDetails);

            var expectedServiceMessage = new ServiceMessage(
                KubernetesDiscoveryCommand.CreateKubernetesTargetServiceMessageName,
                properties);

            Log.ServiceMessages.Should()
                .ContainSingle(s =>
                    s.Name == KubernetesDiscoveryCommand.CreateKubernetesTargetServiceMessageName &&
                    s.Properties["name"] == properties["name"])
                .Which.Should()
                .BeEquivalentTo(expectedServiceMessage);
        }

        protected void ExecuteDiscoveryCommandAndVerifyResult<TAuthenticationDetails>(
            TargetDiscoveryContext<TAuthenticationDetails> discoveryContext)
            where TAuthenticationDetails : class, ITargetDiscoveryAuthenticationDetails
        {
            variables.Add(KubernetesDiscoveryCommand.ContextVariableName,
                JsonConvert.SerializeObject(discoveryContext));

            ExecuteCommandAndVerifyResult(KubernetesDiscoveryCommand.Name);
        }

        private void SetTestClusterVariables()
        {
            variables.Set(KubernetesSpecialVariables.Namespace, TestNamespace);
            variables.Set(ScriptVariables.Syntax, CalamariEnvironment.IsRunningOnWindows ? ScriptSyntax.PowerShell.ToString() : ScriptSyntax.Bash.ToString());
        }

        private void SetInlineScriptVariables(string script)
        {
            SetInlineScriptVariables(script, script);
        }

        private void SetInlineScriptVariables(string bashScript, string powershellScript)
        {
            variables.Set(SpecialVariables.Action.Script.ScriptBodyBySyntax(ScriptSyntax.Bash),
                bashScript);
            variables.Set(SpecialVariables.Action.Script.ScriptBodyBySyntax(ScriptSyntax.PowerShell),
                powershellScript);
        }

        private CalamariResult ExecuteCommand(string command, string workingDirectory, string packagePath)
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