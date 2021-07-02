using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Assent;
using Calamari.Aws.Integration;
using Calamari.Common.Features.EmbeddedResources;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;
using Calamari.Testing.Helpers;
using Calamari.Tests.Fixtures.Integration.FileSystem;
using FluentAssertions;
using NUnit.Framework;
using CalamariResult = Calamari.Tests.Helpers.CalamariResult;
using CaptureCommandInvocationOutputSink = Calamari.Tests.Helpers.CaptureCommandInvocationOutputSink;

namespace Calamari.Tests.KubernetesFixtures
{
    [TestFixture]
    public class KubernetesContextScriptWrapperFixture
    {
        const string ServerUrl = "<server>";
        const string ClusterToken = "mytoken";

        IVariables variables;
        InMemoryLog log;
        Dictionary<string, string> redactMap;

        [SetUp]
        public void Setup()
        {
            variables = new CalamariVariables();
            log = new DoNotDoubleLog();
            redactMap = new Dictionary<string, string>();

            SetTestClusterVariables();
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
        public void ExecutionShouldFailWhenAccountTypeNotValid()
        {
            variables.Set(Deployment.SpecialVariables.Account.AccountType, "not valid");
            variables.Set(ScriptVariables.Syntax, ScriptSyntax.PowerShell.ToString());
            variables.Set(PowerShellVariables.Edition, "Desktop");
            var wrapper = CreateWrapper();
            TestScriptInReadOnlyMode(wrapper).AssertFailure();
        }

        [Test]
        public void ExecutionShouldApplyChmodInBash()
        {
            variables.Set(ScriptVariables.Syntax, ScriptSyntax.Bash.ToString());
            variables.Set(PowerShellVariables.Edition, "Desktop");
            variables.Set(Deployment.SpecialVariables.Account.AccountType, "Token");
            variables.Set(Deployment.SpecialVariables.Account.Token, ClusterToken);
            var wrapper = CreateWrapper();
            TestScriptInReadOnlyMode(wrapper).AssertSuccess();
        }

        [Test]
        public void ExecutionShouldUseGivenNamespace()
        {
            variables.Set(ScriptVariables.Syntax, ScriptSyntax.PowerShell.ToString());
            variables.Set(PowerShellVariables.Edition, "Desktop");
            variables.Set(Deployment.SpecialVariables.Account.AccountType, "Token");
            variables.Set(Deployment.SpecialVariables.Account.Token, ClusterToken);
            variables.Set(SpecialVariables.Namespace, "my-special-namespace");
            var wrapper = CreateWrapper();
            TestScriptInReadOnlyMode(wrapper).AssertSuccess();
        }

        [Test]
        public void ExecutionShouldOutputKubeConfig()
        {
            variables.Set(ScriptVariables.Syntax, ScriptSyntax.PowerShell.ToString());
            variables.Set(PowerShellVariables.Edition, "Desktop");
            variables.Set(Deployment.SpecialVariables.Account.AccountType, "Token");
            variables.Set(Deployment.SpecialVariables.Account.Token, ClusterToken);
            variables.Set(SpecialVariables.OutputKubeConfig, Boolean.TrueString);
            var wrapper = CreateWrapper();
            TestScriptInReadOnlyMode(wrapper).AssertSuccess();
        }

        [Test]
        public void ExecutionWithCustomKubectlExecutable_FileDoesNotExist()
        {
            variables.Set(ScriptVariables.Syntax, ScriptSyntax.Bash.ToString());
            variables.Set(PowerShellVariables.Edition, "Desktop");
            variables.Set(Deployment.SpecialVariables.Account.AccountType, "Token");
            variables.Set(Deployment.SpecialVariables.Account.Token, ClusterToken);
            variables.Set("Octopus.Action.Kubernetes.CustomKubectlExecutable", "mykubectl");
            var wrapper = CreateWrapper();
            TestScriptInReadOnlyMode(wrapper).AssertFailure();
        }

        [Test]
        public void ExecutionWithAzureServicePrincipal()
        {
            variables.Set(ScriptVariables.Syntax, ScriptSyntax.Bash.ToString());
            variables.Set(PowerShellVariables.Edition, "Desktop");
            variables.Set(Deployment.SpecialVariables.Account.AccountType, "AzureServicePrincipal");
            variables.Set("Octopus.Action.Kubernetes.AksClusterResourceGroup", "clusterRG");
            variables.Set(SpecialVariables.AksClusterName, "asCluster");
            variables.Set("Octopus.Action.Kubernetes.AksAdminLogin", Boolean.FalseString);
            variables.Set("Octopus.Action.Azure.SubscriptionId", "azSubscriptionId");
            variables.Set("Octopus.Action.Azure.TenantId", "azTenantId");
            variables.Set("Octopus.Action.Azure.Password", "azPassword");
            variables.Set("Octopus.Action.Azure.ClientId", "azClientId");
            var wrapper = CreateWrapper();
            TestScriptInReadOnlyMode(wrapper).AssertSuccess();
        }

        [Test]
        public void ExecutionWithAzureServicePrincipalWithAdmin()
        {
            variables.Set(ScriptVariables.Syntax, ScriptSyntax.PowerShell.ToString());
            variables.Set(PowerShellVariables.Edition, "Desktop");
            variables.Set(Deployment.SpecialVariables.Account.AccountType, "AzureServicePrincipal");
            variables.Set("Octopus.Action.Kubernetes.AksClusterResourceGroup", "clusterRG");
            variables.Set(SpecialVariables.AksClusterName, "asCluster");
            variables.Set("Octopus.Action.Kubernetes.AksAdminLogin", Boolean.TrueString);
            variables.Set("Octopus.Action.Azure.SubscriptionId", "azSubscriptionId");
            variables.Set("Octopus.Action.Azure.TenantId", "azTenantId");
            variables.Set("Octopus.Action.Azure.Password", "azPassword");
            variables.Set("Octopus.Action.Azure.ClientId", "azClientId");
            var wrapper = CreateWrapper();
            TestScriptInReadOnlyMode(wrapper).AssertSuccess();
        }

        [Test]
        public void ExecutionUsingPodServiceAccount_WithoutServerCert()
        {
            variables.Set(ScriptVariables.Syntax, ScriptSyntax.PowerShell.ToString());
            variables.Set(PowerShellVariables.Edition, "Desktop");
            using (var dir = TemporaryDirectory.Create())
            using (var podServiceAccountToken = new TemporaryFile(Path.Combine(dir.DirectoryPath, "podServiceAccountToken")))
            {
                File.WriteAllText(podServiceAccountToken.FilePath, "podServiceAccountToken");
                redactMap[podServiceAccountToken.FilePath] = "<podServiceAccountTokenPath>";
                variables.Set("Octopus.Action.Kubernetes.PodServiceAccountTokenPath", podServiceAccountToken.FilePath);
                var wrapper = CreateWrapper();
                TestScriptInReadOnlyMode(wrapper).AssertSuccess();
            }
        }

        [Test]
        public void ExecutionUsingPodServiceAccount_WithServerCert()
        {
            variables.Set(ScriptVariables.Syntax, ScriptSyntax.PowerShell.ToString());
            variables.Set(PowerShellVariables.Edition, "Desktop");
            using (var dir = TemporaryDirectory.Create())
            using (var podServiceAccountToken = new TemporaryFile(Path.Combine(dir.DirectoryPath, "podServiceAccountToken")))
            using (var certificateAuthority = new TemporaryFile(Path.Combine(dir.DirectoryPath, "certificateAuthority")))
            {
                File.WriteAllText(podServiceAccountToken.FilePath, "podServiceAccountToken");
                File.WriteAllText(certificateAuthority.FilePath, "certificateAuthority");
                redactMap[podServiceAccountToken.FilePath] = "<podServiceAccountTokenPath>";
                redactMap[certificateAuthority.FilePath] = "<certificateAuthorityPath>";
                variables.Set("Octopus.Action.Kubernetes.PodServiceAccountTokenPath", podServiceAccountToken.FilePath);
                variables.Set("Octopus.Action.Kubernetes.CertificateAuthorityPath", certificateAuthority.FilePath);
                var wrapper = CreateWrapper();
                TestScriptInReadOnlyMode(wrapper).AssertSuccess();
            }
        }

        [Test]
        public void ExecutionWithClientAndCertificateAuthority()
        {
            variables.Set(ScriptVariables.Syntax, ScriptSyntax.Bash.ToString());
            variables.Set(PowerShellVariables.Edition, "Desktop");
            var clientCert = "myclientcert";
            variables.Set("Octopus.Action.Kubernetes.ClientCertificate", clientCert);
            variables.Set($"{clientCert}.CertificatePem", "data");
            variables.Set($"{clientCert}.PrivateKeyPem", "data");
            var certificateAuthority = "myauthority";
            variables.Set("Octopus.Action.Kubernetes.CertificateAuthority", certificateAuthority);
            variables.Set($"{certificateAuthority}.CertificatePem", "data");
            var wrapper = CreateWrapper();
            TestScriptInReadOnlyMode(wrapper).AssertSuccess();
        }

        [Test]
        public void ExecutionWithUsernamePassword()
        {
            variables.Set(ScriptVariables.Syntax, ScriptSyntax.Bash.ToString());
            variables.Set(PowerShellVariables.Edition, "Desktop");
            variables.Set(Deployment.SpecialVariables.Account.AccountType, "UsernamePassword");
            variables.Set("Octopus.Account.Username", "myusername");
            variables.Set("Octopus.Account.Password", "mypassword");
            var wrapper = CreateWrapper();
            TestScriptInReadOnlyMode(wrapper).AssertSuccess();
        }

        [Test]
        public void ExecutionWithEKS()
        {
            variables.Set(ScriptVariables.Syntax, ScriptSyntax.Bash.ToString());
            variables.Set(PowerShellVariables.Edition, "Desktop");
            variables.Set(Deployment.SpecialVariables.Account.AccountType, "AmazonWebServicesAccount");
            variables.Set(SpecialVariables.EksClusterName, "my-eks-cluster");
            var account = "eks_account";
            variables.Set("Octopus.Action.AwsAccount.Variable", account);
            variables.Set("Octopus.Action.Aws.Region", "eks_region");
            variables.Set($"{account}.AccessKey", "eksAccessKey");
            variables.Set($"{account}.SecretKey", "eksSecretKey");
            var wrapper = CreateWrapper();
            TestScriptInReadOnlyMode(wrapper).AssertSuccess();
        }
        
        [Test]
        public void ExecutionWithGoogleCloudAccount_WhenZoneIsProvided()
        {
            variables.Set(Deployment.SpecialVariables.Account.AccountType, "GoogleCloudAccount");
            variables.Set(SpecialVariables.GkeClusterName, "gke-cluster-name");
            var account = "gke_account";
            variables.Set("Octopus.Action.GoogleCloudAccount.Variable", account);
            var jsonKey = Environment.GetEnvironmentVariable("GOOGLECLOUD_OCTOPUSAPITESTER_JSONKEY") ?? string.Empty;
            variables.Set($"{account}.JsonKey", Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonKey)));
            variables.Set("Octopus.Action.GoogleCloud.Project", "gke-project");
            variables.Set("Octopus.Action.GoogleCloud.Zone", "gke-zone");
            var wrapper = CreateWrapper();
            TestScriptInReadOnlyMode(wrapper).AssertSuccess();
        }
        
        [Test]
        public void ExecutionWithGoogleCloudAccount_WhenRegionIsProvided()
        {
            variables.Set(Deployment.SpecialVariables.Account.AccountType, "GoogleCloudAccount");
            variables.Set(SpecialVariables.GkeClusterName, "gke-cluster-name");
            var account = "gke_account";
            variables.Set("Octopus.Action.GoogleCloudAccount.Variable", account);
            var jsonKey = Environment.GetEnvironmentVariable("GOOGLECLOUD_OCTOPUSAPITESTER_JSONKEY") ?? string.Empty;
            variables.Set($"{account}.JsonKey", Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonKey)));
            variables.Set("Octopus.Action.GoogleCloud.Project", "gke-project");
            variables.Set("Octopus.Action.GoogleCloud.Region", "gke-region");
            var wrapper = CreateWrapper();
            TestScriptInReadOnlyMode(wrapper).AssertSuccess();
        }
        
        [Test]
        public void ExecutionWithGoogleCloudAccount_WhenBothZoneAndRegionAreProvided()
        {
            variables.Set(Deployment.SpecialVariables.Account.AccountType, "GoogleCloudAccount");
            variables.Set(SpecialVariables.GkeClusterName, "gke-cluster-name");
            var account = "gke_account";
            variables.Set("Octopus.Action.GoogleCloudAccount.Variable", account);
            var jsonKey = Environment.GetEnvironmentVariable("GOOGLECLOUD_OCTOPUSAPITESTER_JSONKEY") ?? string.Empty;
            variables.Set($"{account}.JsonKey", Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonKey)));
            variables.Set("Octopus.Action.GoogleCloud.Project", "gke-project");
            variables.Set("Octopus.Action.GoogleCloud.Region", "gke-region");
            variables.Set("Octopus.Action.GoogleCloud.Zone", "gke-zone");
            var wrapper = CreateWrapper();
            TestScriptInReadOnlyMode(wrapper).AssertSuccess();
        }
        
        [Test]
        public void ExecutionWithGoogleCloudAccount_WhenNeitherZoneOrRegionAreProvided()
        {
            variables.Set(Deployment.SpecialVariables.Account.AccountType, "GoogleCloudAccount");
            variables.Set(SpecialVariables.GkeClusterName, "gke-cluster-name");
            var account = "gke_account";
            variables.Set("Octopus.Action.GoogleCloudAccount.Variable", account);
            var jsonKey = Environment.GetEnvironmentVariable("GOOGLECLOUD_OCTOPUSAPITESTER_JSONKEY") ?? string.Empty;
            variables.Set($"{account}.JsonKey", Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonKey)));
            variables.Set("Octopus.Action.GoogleCloud.Project", "gke-project");
            var wrapper = CreateWrapper();
            Assert.Throws<ArgumentException>(() => TestScriptInReadOnlyMode(wrapper));
        }

        [Test]
        public void ExecutionWithCustomKubectlExecutable_FileExists()
        {
            variables.Set(ScriptVariables.Syntax, ScriptSyntax.PowerShell.ToString());
            variables.Set(PowerShellVariables.Edition, "Desktop");
            variables.Set(Deployment.SpecialVariables.Account.AccountType, "Token");
            variables.Set(Deployment.SpecialVariables.Account.Token, ClusterToken);
            using (var dir = TemporaryDirectory.Create())
            using (var tempExe = new TemporaryFile(Path.Combine(dir.DirectoryPath, "mykubectl.exe")))
            {
                File.WriteAllText(tempExe.FilePath, string.Empty);
                variables.Set("Octopus.Action.Kubernetes.CustomKubectlExecutable", tempExe.FilePath);
                redactMap[tempExe.FilePath] = "<customkubectl>";
                var wrapper = CreateWrapper();
                TestScriptInReadOnlyMode(wrapper).AssertSuccess();
            }
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
        }

        CalamariResult TestScriptInReadOnlyMode(IScriptWrapper wrapper, [CallerMemberName] string testName = null, [CallerFilePath] string filePath = null)
        {
            using (var dir = TemporaryDirectory.Create())
            using (var temp = new TemporaryFile(Path.Combine(dir.DirectoryPath, $"scriptName.{(variables.Get(ScriptVariables.Syntax) == ScriptSyntax.Bash.ToString() ? "sh" : "ps1")}")))
            {
                File.WriteAllText(temp.FilePath, "kubectl get nodes");

                var output = ExecuteScriptInRecordOnlyMode(wrapper, temp.FilePath);
                redactMap[dir.DirectoryPath + Path.DirectorySeparatorChar] = "<path>";
                var sb = new StringBuilder();
                foreach (var message in log.Messages)
                {
                    var text = message.FormattedMessage;
                    text = redactMap.Aggregate(text, (current, pair) => current.Replace(pair.Key, pair.Value));
                    sb.AppendLine($"[{message.Level}] {text}");
                }

                this.Assent(sb.ToString().Replace("\r\n", "\n"), testName: testName, filePath: filePath, configuration: Helpers.TestEnvironment.AssentConfiguration);

                return output;
            }
        }

        CalamariResult ExecuteScriptInRecordOnlyMode(IScriptWrapper wrapper, string scriptName)
        {
            return ExecuteScriptInternal(new RecordOnly(), wrapper, scriptName);
        }

        CalamariResult ExecuteScriptInternal(ICommandLineRunner runner, IScriptWrapper wrapper, string scriptName)
        {
            var wrappers = new List<IScriptWrapper>(new[] { wrapper });
            if (variables.Get(Deployment.SpecialVariables.Account.AccountType) == "AmazonWebServicesAccount")
            {
                wrappers.Add(new AwsScriptWrapper(log, variables) { VerifyAmazonLogin = () => Task.FromResult(true) });
            }

            var engine = new ScriptEngine(wrappers);
            var result = engine.Execute(new Script(scriptName), variables, runner, new Dictionary<string, string>());

            return new CalamariResult(result.ExitCode, new CaptureCommandInvocationOutputSink());
        }

        class RecordOnly :  ICommandLineRunner
        {
            public CommandResult Execute(CommandLineInvocation invocation)
            {
                // We only want to output executable string. eg. ExecuteCommandAndReturnOutput("where", "kubectl.exe")
                if (new string[] { "kubectl", "az", "gcloud", "kubectl.exe", "az.cmd", "gcloud.cmd" }.Contains(invocation.Arguments)) 
                    invocation.AdditionalInvocationOutputSink?.WriteInfo(Path.GetFileNameWithoutExtension(invocation.Arguments));
                return new CommandResult(invocation.ToString(), 0);
            }
        }
    }
}
