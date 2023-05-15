#if NETCORE
using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Aws.Integration;
using Calamari.Aws.Kubernetes.Discovery;
using Calamari.CloudAccounts;
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

        protected KubernetesContextScriptWrapper CreateWrapper()
        {
            return new KubernetesContextScriptWrapper(variables, Log, new AwsEnvironmentVariablesFactory(),
                new AssemblyEmbeddedResources(), new TestCalamariPhysicalFileSystem());
        }

        void SetTestClusterVariables()
        {

            variables.Set(SpecialVariables.Namespace, TestNamespace);
            variables.Set(ScriptVariables.Syntax, CalamariEnvironment.IsRunningOnWindows ? ScriptSyntax.PowerShell.ToString() : ScriptSyntax.Bash.ToString());
        }

        CalamariResult ExecuteScript(IScriptWrapper wrapper, string scriptName)
        {
            var calamariResult = ExecuteScriptInternal(new CommandLineRunner(Log, variables), wrapper, scriptName);

            WriteLogMessagesToTestOutput();

            return calamariResult;
        }

        CalamariResult ExecuteScriptInternal(ICommandLineRunner runner, IScriptWrapper wrapper, string scriptName)
        {
            var wrappers = new List<IScriptWrapper>(new[] { wrapper });
            if (variables.Get(Deployment.SpecialVariables.Account.AccountType) == "AmazonWebServicesAccount")
            {
                wrappers.Add(new AwsScriptWrapper(Log, variables));
            }

            var engine = new ScriptEngine(wrappers);
            var result = engine.Execute(new Script(scriptName), variables, runner, GetEnvironments());

            return new CalamariResult(result.ExitCode, new CaptureCommandInvocationOutputSink());
        }

        protected virtual Dictionary<string, string> GetEnvironments()
        {
            return new Dictionary<string, string>();
        }

        protected void TestScript(IScriptWrapper wrapper, string scriptName)
        {
            using (var dir = TemporaryDirectory.Create())
            {
                var folderPath = Path.Combine(dir.DirectoryPath, "Folder with spaces");

                using (var temp = new TemporaryFile(Path.Combine(folderPath, $"{scriptName}.{(variables.Get(ScriptVariables.Syntax) == ScriptSyntax.Bash.ToString() ? "sh" : "ps1")}")))
                {
                    Directory.CreateDirectory(folderPath);
                    File.WriteAllText(temp.FilePath, $"echo running target script...");

                    var output = ExecuteScript(wrapper, temp.FilePath);
                    output.AssertSuccess();
                }
            }
        }

        protected void TestScriptAndVerifyCluster(IScriptWrapper wrapper, string scriptName, string kubectlExe = "kubectl")
        {
            using (var dir = TemporaryDirectory.Create())
            {
                var folderPath = Path.Combine(dir.DirectoryPath, "Folder with spaces");

                using (var temp = new TemporaryFile(Path.Combine(folderPath, $"{scriptName}.{(variables.Get(ScriptVariables.Syntax) == ScriptSyntax.Bash.ToString() ? "sh" : "ps1")}")))
                {
                    Directory.CreateDirectory(folderPath);
                    File.WriteAllText(temp.FilePath, $"{kubectlExe} cluster-info");

                    var output = ExecuteScript(wrapper, temp.FilePath);
                    output.AssertSuccess();
                }
            }
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

            var result =
                ExecuteDiscoveryCommand(targetDiscoveryContext,
                    new[]{"Calamari.Aws"}
                );

            result.AssertSuccess();
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

        protected CalamariResult ExecuteDiscoveryCommand<TAuthenticationDetails>(
            TargetDiscoveryContext<TAuthenticationDetails> discoveryContext,
            IEnumerable<string> extensions,
            params (string key, string value)[] otherVariables)
            where TAuthenticationDetails : class, ITargetDiscoveryAuthenticationDetails
        {
            using (var variablesFile = new TemporaryFile(Path.GetTempFileName()))
            {
                variables.Add(KubernetesDiscoveryCommand.ContextVariableName, JsonConvert.SerializeObject(discoveryContext));
                foreach (var (key, value) in otherVariables)
                    variables.Add(key, value);

                variables.Save(variablesFile.FilePath);

                var result = InvokeInProcess(Calamari()
                       .Action(KubernetesDiscoveryCommand.Name)
                       .Argument("variables", variablesFile.FilePath)
                       .Argument("extensions", string.Join(',', extensions)));

                WriteLogMessagesToTestOutput();

                return result;
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