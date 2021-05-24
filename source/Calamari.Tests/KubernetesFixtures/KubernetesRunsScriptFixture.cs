using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripting.WindowsPowerShell;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;
using Calamari.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures
{
    [TestFixture]
    public class KubernetesContextScriptWrapperFixture
    {
        static readonly string ServerUrl = Environment.GetEnvironmentVariable("K8S_OctopusAPITester_Server");
        static readonly string ClusterToken = Environment.GetEnvironmentVariable("K8S_OctopusAPITester_Token");
        protected IVariables Variables { get; set; } = new CalamariVariables();

        [Test]
        [TestCase("Url", "", "", true)]
        [TestCase("", "Name", "" , true)]
        [TestCase("", "", "Name", true)]
        [TestCase("", "", "", false)]
        public void ShouldBeEnabledIfAnyVariablesAreProvided(string clusterUrl, string aksClusterName,
            string eksClusterName, bool expected)
        {
            var variables = new CalamariVariables
            {
                {Kubernetes.SpecialVariables.ClusterUrl, clusterUrl},
                {Kubernetes.SpecialVariables.AksClusterName, aksClusterName},
                {Kubernetes.SpecialVariables.EksClusterName, eksClusterName}
            };
            var target = new KubernetesContextScriptWrapper(variables);
            var actual = target.IsEnabled(ScriptSyntaxHelper.GetPreferredScriptSyntaxForEnvironment());
            actual.Should().Be(expected);
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public void WindowsPowershellKubeCtlScripts()
        {
            SetTestClusterVariables();
            Variables.Set(PowerShellVariables.Edition, "Desktop");
            var wrapper = new KubernetesContextScriptWrapper(Variables);
            TestScript(wrapper, "Test-Script.ps1");
        }

        [Test]
        public void PowershellCoreKubeCtlScripts()
        {
            SetTestClusterVariables();
            Variables.Set(PowerShellVariables.Edition, "Core");
            var wrapper = new KubernetesContextScriptWrapper(Variables);
            TestScript(wrapper, "Test-Script.ps1");
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyNix)]
        public void BashKubeCtlScripts()
        {
            SetTestClusterVariables();
            var wrapper = new KubernetesContextScriptWrapper(Variables);
            TestScript(wrapper, "Test-Script.sh");
        }

        private void SetTestClusterVariables()
        {
            Variables.Set(Kubernetes.SpecialVariables.ClusterUrl, ServerUrl);
            Variables.Set(Kubernetes.SpecialVariables.SkipTlsVerification, "true");
            Variables.Set(Kubernetes.SpecialVariables.Namespace, "calamari-testing");
            Variables.Set(Deployment.SpecialVariables.Account.AccountType, "Token");
            Variables.Set(Deployment.SpecialVariables.Account.Token, ClusterToken);
        }

        void TestScript(IScriptWrapper wrapper, string scriptName)
        {
            using (var dir = TemporaryDirectory.Create())
            using (var temp = new TemporaryFile(Path.Combine(dir.DirectoryPath, scriptName)))
            {
                File.WriteAllText(temp.FilePath, "kubectl get nodes");

                var output = ExecuteScript(wrapper, temp.FilePath, Variables);
                output.AssertSuccess();
            }
        }

        CalamariResult ExecuteScript(IScriptWrapper wrapper, string scriptName, IVariables variables)
        {
            var runner = new TestCommandLineRunner(ConsoleLog.Instance, variables);
            wrapper.NextWrapper = new TerminalScriptWrapper(new PowerShellScriptExecutor(), variables);
            var result = wrapper.ExecuteScript(new Script(scriptName), ScriptSyntax.PowerShell, runner, new Dictionary<string, string>());
            return new CalamariResult(result.ExitCode, runner.Output);
        }
    }
}