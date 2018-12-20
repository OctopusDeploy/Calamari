using System;
using System.Collections.Specialized;
using System.IO;
using Calamari.Hooks;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Calamari.Integration.Scripting.WindowsPowerShell;
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

        [Test]
        [TestCase("Url", "", "", ScriptSyntax.PowerShell, true)]
        [TestCase("", "Name", "", ScriptSyntax.PowerShell, true)]
        [TestCase("", "", "Name", ScriptSyntax.PowerShell, true)]
        [TestCase("Url", "", "", ScriptSyntax.Bash, true)]
        [TestCase("", "", "", ScriptSyntax.PowerShell, false)]
        [TestCase("Url", "", "", ScriptSyntax.FSharp, false)]
        public void ShouldBeEnabled(string clusterUrl, string aksClusterName, string eksClusterName, ScriptSyntax syntax, bool expected)
        {
            var variables = new CalamariVariableDictionary
            {
                {SpecialVariables.ClusterUrl, clusterUrl},
                {SpecialVariables.AksClusterName, aksClusterName},
                {SpecialVariables.EksClusterName, eksClusterName}
            };
            var target = new KubernetesContextScriptWrapper(variables);
            var actual = target.IsEnabled(syntax);
            actual.Should().Be(expected);
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        [Ignore("Not yet ready for prime time. Tested via Helm tests atm anyway")]
        public void PowershellKubeCtlScripts()
        {
            var wrapper = new KubernetesContextScriptWrapper(new CalamariVariableDictionary());
            TestScript(wrapper, "Test-Script.ps1");
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Nix)]
        [Ignore("Not yet ready for prime time. Tested via Helm tests atm anyway")]
        public void BashKubeCtlScripts()
        {
            var wrapper = new KubernetesContextScriptWrapper(new CalamariVariableDictionary());
            TestScript(wrapper, "Test-Script.sh");
        }

        void TestScript(IScriptWrapper wrapper, string scriptName)
        {
            using (var dir = TemporaryDirectory.Create())
            using (var temp = new TemporaryFile(Path.Combine(dir.DirectoryPath, scriptName)))
            {
                File.WriteAllText(temp.FilePath, "kubectl get nodes");

                var deploymentVariables = new CalamariVariableDictionary();
                deploymentVariables.Set(SpecialVariables.ClusterUrl, ServerUrl);
                
                deploymentVariables.Set(SpecialVariables.SkipTlsVerification, "true");
                deploymentVariables.Set(SpecialVariables.Namespace, "calamari-testing");
                deploymentVariables.Set(Deployment.SpecialVariables.Account.AccountType, "Token");
                deploymentVariables.Set(Deployment.SpecialVariables.Account.Token, ClusterToken);
                
                var output = ExecuteScript(wrapper, temp.FilePath, deploymentVariables);
                output.AssertSuccess();
            }
        }

        CalamariResult ExecuteScript(IScriptWrapper wrapper, string scriptName, CalamariVariableDictionary variables)
        {
            var capture = new CaptureCommandOutput();
            var runner = new CommandLineRunner(capture);
            wrapper.NextWrapper = new TerminalScriptWrapper(new PowerShellScriptEngine());
            var result = wrapper.ExecuteScript(new Script(scriptName), ScriptSyntax.PowerShell, variables, runner, new StringDictionary());
            //var result = psse.Execute(new Script(scriptName), variables, runner);
            return new CalamariResult(result.ExitCode, capture);
        }
    }
}