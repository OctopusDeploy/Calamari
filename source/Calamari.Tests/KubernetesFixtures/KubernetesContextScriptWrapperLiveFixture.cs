using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Calamari.Aws.Integration;
using Calamari.Common.Features.EmbeddedResources;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;
using Calamari.Testing.Helpers;
using Calamari.Tests.Fixtures;
using Calamari.Tests.Fixtures.Integration.FileSystem;
using FluentAssertions;
using NUnit.Framework;
using CalamariResult = Calamari.Tests.Helpers.CalamariResult;
using CaptureCommandInvocationOutputSink = Calamari.Tests.Helpers.CaptureCommandInvocationOutputSink;
using TestCategory = Calamari.Tests.Helpers.TestCategory;

namespace Calamari.Tests.KubernetesFixtures
{
    [TestFixture]
    public class KubernetesContextScriptWrapperLiveFixture
    {
        static readonly string ServerUrl = Environment.GetEnvironmentVariable("K8S_OctopusAPITester_Server");
        static readonly string ClusterToken = Environment.GetEnvironmentVariable("K8S_OctopusAPITester_Token");

        IVariables variables;
        InMemoryLog log;
        InstallTools installTools;

        [SetUp]
        public async Task Setup()
        {
            variables = new CalamariVariables();
            log = new DoNotDoubleLog();

            installTools = new InstallTools();
            await installTools.Install();

            SetTestClusterVariables();
        }

        [Test]
        public async Task InstallTools()
        {
            installTools = new InstallTools();
            await installTools.Install();

            installTools.KubectlExecutable.Should().NotBeNullOrEmpty();
            installTools.TerraformExecutable.Should().NotBeNullOrEmpty();
            installTools.AwsAuthenticatorExecutable.Should().NotBeNullOrEmpty();
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        [RequiresPowerShell5OrLower]
        public void WindowsPowershellKubeCtlScripts()
        {
            variables.Set(ScriptVariables.Syntax, ScriptSyntax.PowerShell.ToString());
            variables.Set(PowerShellVariables.Edition, "Desktop");
            variables.Set(Deployment.SpecialVariables.Account.AccountType, "Token");
            variables.Set(Deployment.SpecialVariables.Account.Token, ClusterToken);
            var wrapper = CreateWrapper();
            TestScript(wrapper, "Test-Script.ps1");
        }

        [Test]
        [RequiresNonFreeBSDPlatform]
        [RequiresPowerShellCore]
        public void PowershellCoreKubeCtlScripts()
        {
            variables.Set(ScriptVariables.Syntax, ScriptSyntax.PowerShell.ToString());
            variables.Set(PowerShellVariables.Edition, "Core");
            variables.Set(Deployment.SpecialVariables.Account.AccountType, "Token");
            variables.Set(Deployment.SpecialVariables.Account.Token, ClusterToken);
            var wrapper = CreateWrapper();
            TestScript(wrapper, "Test-Script.ps1");
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyNix)]
        [Platform("Linux")]
        [RequiresNonFreeBSDPlatform]
        public void BashKubeCtlScripts()
        {
            variables.Set(Deployment.SpecialVariables.Account.AccountType, "Token");
            variables.Set(Deployment.SpecialVariables.Account.Token, ClusterToken);
            variables.Set(ScriptVariables.Syntax, ScriptSyntax.Bash.ToString());
            var wrapper = CreateWrapper();
            TestScript(wrapper, "Test-Script.sh");
        }

        KubernetesContextScriptWrapper CreateWrapper()
        {
            return new KubernetesContextScriptWrapper(variables, log, new AssemblyEmbeddedResources(), new TestCalamariPhysicalFileSystem());
        }

        void SetTestClusterVariables()
        {
            variables.Set(SpecialVariables.ClusterUrl, ServerUrl);
            variables.Set(SpecialVariables.SkipTlsVerification, "true");
            variables.Set(SpecialVariables.Namespace, "calamari-testing");
            variables.Set("Octopus.Action.Kubernetes.CustomKubectlExecutable", installTools.KubectlExecutable);
        }

        CalamariResult ExecuteScript(IScriptWrapper wrapper, string scriptName)
        {
            var calamariResult = ExecuteScriptInternal(new CommandLineRunner(log, variables), wrapper, scriptName);

            foreach (var message in log.Messages)
            {
                Console.WriteLine($"[{message.Level}] {message.FormattedMessage}");
            }

            return calamariResult;
        }

        CalamariResult ExecuteScriptInternal(ICommandLineRunner runner, IScriptWrapper wrapper, string scriptName)
        {
            var wrappers = new List<IScriptWrapper>(new[] { wrapper });
            if (variables.Get(Deployment.SpecialVariables.Account.AccountType) == "AmazonWebServicesAccount")
            {
                wrappers.Add(new AwsScriptWrapper(log, variables) { VerifyAmazonLogin = () => Task.FromResult(true) });
            }

            var engine = new ScriptEngine(wrappers);
            var currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            var delimiter = CalamariEnvironment.IsRunningOnWindows ? ";" : ":";
            if (currentPath.Length > 0 && currentPath.EndsWith(delimiter))
            {
                currentPath += delimiter;
            }
            currentPath += Path.GetDirectoryName(installTools.AwsAuthenticatorExecutable);
            var result = engine.Execute(new Script(scriptName), variables, runner, new Dictionary<string, string> { { "PATH", currentPath } });

            return new CalamariResult(result.ExitCode, new CaptureCommandInvocationOutputSink());
        }

        void TestScript(IScriptWrapper wrapper, string scriptName)
        {
            using (var dir = TemporaryDirectory.Create())
            using (var temp = new TemporaryFile(Path.Combine(dir.DirectoryPath, scriptName)))
            {
                File.WriteAllText(temp.FilePath, "kubectl get nodes");

                var output = ExecuteScript(wrapper, temp.FilePath);
                output.AssertSuccess();
            }
        }
    }
}