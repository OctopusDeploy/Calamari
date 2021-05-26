using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Common.Features.EmbeddedResources;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripts;
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
    public class KubernetesContextScriptWrapperFixture
    {
        static readonly string ServerUrl = Environment.GetEnvironmentVariable("K8S_OctopusAPITester_Server");
        static readonly string ClusterToken = Environment.GetEnvironmentVariable("K8S_OctopusAPITester_Token");
        IVariables variables;
        InMemoryLog log;

        [SetUp]
        public void Setup()
        {
            variables = new CalamariVariables();
            log = new DoNotDoubleLog();
        }

        [Test]
        [TestCase("Url", "", "", true)]
        [TestCase("", "Name", "" , true)]
        [TestCase("", "", "Name", true)]
        [TestCase("", "", "", false)]
        public void ShouldBeEnabledIfAnyVariablesAreProvided(string clusterUrl, string aksClusterName,
            string eksClusterName, bool expected)
        {
            variables.Set(SpecialVariables.ClusterUrl, clusterUrl);
            variables.Set(SpecialVariables.AksClusterName, aksClusterName);
            variables.Set(SpecialVariables.EksClusterName, eksClusterName);

            var wrapper = CreateWrapper();
            var actual = wrapper.IsEnabled(ScriptSyntaxHelper.GetPreferredScriptSyntaxForEnvironment());
            actual.Should().Be(expected);
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        [RequiresPowerShell5OrLower]
        public void WindowsPowershellKubeCtlScripts()
        {
            SetTestClusterVariables();
            variables.Set(ScriptVariables.Syntax, ScriptSyntax.PowerShell.ToString());
            variables.Set(PowerShellVariables.Edition, "Desktop");
            var wrapper = CreateWrapper();
            TestScript(wrapper, "Test-Script.ps1");
        }

        [Test]
        public void MakeWrapperFail()
        {
            SetTestClusterVariables();
            variables.Set(Deployment.SpecialVariables.Account.AccountType, "not valid");
            variables.Set(ScriptVariables.Syntax, ScriptSyntax.PowerShell.ToString());
            variables.Set(PowerShellVariables.Edition, "Desktop");
            var wrapper = CreateWrapper();
            var result = ExecuteScript(wrapper, "Does not matter.ps1");

            result.AssertFailure();
        }

        [Test]
        [RequiresNonFreeBSDPlatform]
        [RequiresPowerShellCore]
        public void PowershellCoreKubeCtlScripts()
        {
            SetTestClusterVariables();
            variables.Set(ScriptVariables.Syntax, ScriptSyntax.PowerShell.ToString());
            variables.Set(PowerShellVariables.Edition, "Core");
            var wrapper = CreateWrapper();
            TestScript(wrapper, "Test-Script.ps1");
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyNix)]
        [RequiresNonFreeBSDPlatform]
        public void BashKubeCtlScripts()
        {
            SetTestClusterVariables();
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
            variables.Set(Deployment.SpecialVariables.Account.AccountType, "Token");
            variables.Set(Deployment.SpecialVariables.Account.Token, ClusterToken);
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

        CalamariResult ExecuteScript(IScriptWrapper wrapper, string scriptName)
        {
            var runner = new CommandLineRunner(log, variables);
            var engine = new ScriptEngine(new[] { wrapper });
            var result = engine.Execute(new Script(scriptName), variables, runner, new Dictionary<string, string>());

            foreach (var message in log.Messages)
            {
                Console.WriteLine($"[{message.Level}] {message.FormattedMessage}");
            }

            return new CalamariResult(result.ExitCode, new CaptureCommandInvocationOutputSink());
        }

        class DoNotDoubleLog : InMemoryLog
        {
            protected override void StdErr(string message)
            {
            }

            protected override void StdOut(string message)
            {
            }
        }
    }
}