using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Common.Features.EmbeddedResources;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;
using Calamari.Tests.Fixtures;
using Calamari.Tests.Fixtures.Integration.FileSystem;
using Calamari.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;
using CalamariResult = Calamari.Tests.Helpers.CalamariResult;
using TestCategory = Calamari.Tests.Helpers.TestCategory;
using TestCommandLineRunner = Calamari.Tests.Helpers.TestCommandLineRunner;

namespace Calamari.Tests.KubernetesFixtures
{
    [TestFixture]
    public class KubernetesContextScriptWrapperFixture
    {
        static readonly string ServerUrl = Environment.GetEnvironmentVariable("K8S_OctopusAPITester_Server");
        static readonly string ClusterToken = Environment.GetEnvironmentVariable("K8S_OctopusAPITester_Token");
        IVariables variables;

        [SetUp]
        public void Setup()
        {
            variables = new CalamariVariables();
        }

        [Test]
        [TestCase("Url", "", "", true)]
        [TestCase("", "Name", "" , true)]
        [TestCase("", "", "Name", true)]
        [TestCase("", "", "", false)]
        public void ShouldBeEnabledIfAnyVariablesAreProvided(string clusterUrl, string aksClusterName,
            string eksClusterName, bool expected)
        {
            variables.Set(Kubernetes.SpecialVariables.ClusterUrl, clusterUrl);
            variables.Set(Kubernetes.SpecialVariables.AksClusterName, aksClusterName);
            variables.Set(Kubernetes.SpecialVariables.EksClusterName, eksClusterName);

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
            return new KubernetesContextScriptWrapper(variables, ConsoleLog.Instance, new AssemblyEmbeddedResources(), new TestCalamariPhysicalFileSystem());
        }

        void SetTestClusterVariables()
        {
            variables.Set(Kubernetes.SpecialVariables.ClusterUrl, ServerUrl);
            variables.Set(Kubernetes.SpecialVariables.SkipTlsVerification, "true");
            variables.Set(Kubernetes.SpecialVariables.Namespace, "calamari-testing");
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
            var runner = new CommandLineRunner(ConsoleLog.Instance, variables);
            var engine = new ScriptEngine(new[] { wrapper });
            var result = engine.Execute(new Script(scriptName), variables, runner, new Dictionary<string, string>());
            return new CalamariResult(result.ExitCode, new CaptureCommandInvocationOutputSink());
        }
    }
}