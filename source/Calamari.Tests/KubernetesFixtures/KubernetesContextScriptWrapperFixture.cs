﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Assent;
using Calamari.Aws.Integration;
using Calamari.Common.Features.EmbeddedResources;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripting.DotnetScript;
using Calamari.Common.Features.Scripts;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;
using Calamari.Testing;
using Calamari.Testing.Helpers;
using Calamari.Testing.Requirements;
using Calamari.Tests.Fixtures.Integration.FileSystem;
using Calamari.Tests.Helpers;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures
{
    [TestFixture]
    public class KubernetesContextScriptWrapperFixture
    {
        const string ServerUrl = "<server>";
        const string ClusterToken = "mytoken";

        IVariables variables;
        InMemoryLog log;
        InstallTools installTools;
        Dictionary<string, string> environmentVariables;

        static readonly CancellationTokenSource CancellationTokenSource = new CancellationTokenSource();
        readonly CancellationToken cancellationToken = CancellationTokenSource.Token;

        [SetUp]
        public void Setup()
        {
            variables = new CalamariVariables();
            log = new DoNotDoubleLog();
            environmentVariables = new Dictionary<string, string>();

            SetTestClusterVariables();
        }

        [Test]
        [TestCase("Url", "", "", "", true)]
        [TestCase("", "Name", "", "", true)]
        [TestCase("", "", "Name", "", true)]
        [TestCase("", "", "", "KubernetesTentacle", true)]
        [TestCase("", "", "", "", false)]
        public void ShouldBeEnabledIfAnyVariablesAreProvided(string clusterUrl,
                                                             string aksClusterName,
                                                             string eksClusterName,
                                                             string deploymentTargetType,
                                                             bool expected)
        {
            variables.Set(SpecialVariables.ClusterUrl, clusterUrl);
            variables.Set(SpecialVariables.AksClusterName, aksClusterName);
            variables.Set(SpecialVariables.EksClusterName, eksClusterName);
            variables.Set(MachineVariables.DeploymentTargetType, deploymentTargetType);

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
                variables.Set("Octopus.Action.Kubernetes.PodServiceAccountTokenPath", podServiceAccountToken.FilePath);
                var wrapper = CreateWrapper();
                TestScriptInReadOnlyMode(wrapper, redactMap: new Dictionary<string, string>() { { podServiceAccountToken.FilePath, "<podServiceAccountTokenPath>" } }).AssertSuccess();
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
                var redactMap = new Dictionary<string, string>();
                redactMap[podServiceAccountToken.FilePath] = "<podServiceAccountTokenPath>";
                redactMap[certificateAuthority.FilePath] = "<certificateAuthorityPath>";
                variables.Set("Octopus.Action.Kubernetes.PodServiceAccountTokenPath", podServiceAccountToken.FilePath);
                variables.Set("Octopus.Action.Kubernetes.CertificateAuthorityPath", certificateAuthority.FilePath);
                var wrapper = CreateWrapper();
                TestScriptInReadOnlyMode(wrapper, redactMap: redactMap).AssertSuccess();
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
        [WindowsTest] // This test requires the aws cli tools. Currently only configured to install on Linux & Windows
        public async Task ExecutionWithEKS_IAMAuthenticator()
        {
            await InstallTools(InstallAwsCli);

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
        [WindowsTest] // This test requires the aws cli tools. Currently only configured to install on Windows
        public async Task ExecutionWithEKS_AwsCLIAuthenticator()
        {
            await InstallTools(InstallAwsCli);

            // Overriding the cluster url with a valid url. This is required to hit the aws eks get-token endpoint.
            variables.Set(SpecialVariables.ClusterUrl, "https://someHash.gr7.ap-southeast-2.eks.amazonaws.com");
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
        [WindowsTest] // This test requires the aws cli tools. Currently only configured to install on Windows
        public async Task ExecutionWithEKS_AwsCLIAuthenticator_WithExecFeatureToggleEnabled()
        {
            await InstallTools(InstallAwsCli);

            //set the feature toggle
            variables.SetStrings(KnownVariables.EnabledFeatureToggles,
                                 new[]
                                 {
                                     FeatureToggle.KubernetesAuthAwsCliWithExecFeatureToggle.ToString()
                                 },
                                 ",");

            // Overriding the cluster url with a valid url. This is required to hit the aws eks get-token endpoint.
            variables.Set(SpecialVariables.ClusterUrl, "https://someHash.gr7.ap-southeast-2.eks.amazonaws.com");
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
        [WindowsTest] // This test requires the GKE GCloud Auth plugin. Currently only configured to install on Windows
        public async Task ExecutionWithGoogleCloudAccount_WhenZoneIsProvided()
        {
            await InstallTools(InstallGCloud);

            variables.Set(Deployment.SpecialVariables.Account.AccountType, "GoogleCloudAccount");
            variables.Set(SpecialVariables.GkeClusterName, "gke-cluster-name");
            var account = "gke_account";
            variables.Set("Octopus.Action.GoogleCloudAccount.Variable", account);
            var jsonKey = await ExternalVariables.Get(ExternalVariable.GoogleCloudJsonKeyfile, cancellationToken);
            variables.Set($"{account}.JsonKey", Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonKey)));
            variables.Set("Octopus.Action.GoogleCloud.Project", "gke-project");
            variables.Set("Octopus.Action.GoogleCloud.Zone", "gke-zone");
            var wrapper = CreateWrapper();
            TestScriptInReadOnlyMode(wrapper).AssertSuccess();
        }

        [Test]
        [WindowsTest] // This test requires the GKE GCloud Auth plugin. Currently only configured to install on Windows
        public async Task ExecutionWithGoogleCloudAccount_WhenRegionIsProvided()
        {
            await InstallTools(InstallGCloud);

            variables.Set(Deployment.SpecialVariables.Account.AccountType, "GoogleCloudAccount");
            variables.Set(SpecialVariables.GkeClusterName, "gke-cluster-name");
            var account = "gke_account";
            variables.Set("Octopus.Action.GoogleCloudAccount.Variable", account);
            var jsonKey = await ExternalVariables.Get(ExternalVariable.GoogleCloudJsonKeyfile, cancellationToken);
            variables.Set($"{account}.JsonKey", Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonKey)));
            variables.Set("Octopus.Action.GoogleCloud.Project", "gke-project");
            variables.Set("Octopus.Action.GoogleCloud.Region", "gke-region");
            var wrapper = CreateWrapper();
            TestScriptInReadOnlyMode(wrapper).AssertSuccess();
        }

        [Test]
        [WindowsTest] // This test requires the GKE GCloud Auth plugin. Currently only configured to install on Windows
        public async Task ExecutionWithGoogleCloudAccount_WhenBothZoneAndRegionAreProvided()
        {
            await InstallTools(InstallGCloud);

            variables.Set(Deployment.SpecialVariables.Account.AccountType, "GoogleCloudAccount");
            variables.Set(SpecialVariables.GkeClusterName, "gke-cluster-name");
            var account = "gke_account";
            variables.Set("Octopus.Action.GoogleCloudAccount.Variable", account);
            var jsonKey = await ExternalVariables.Get(ExternalVariable.GoogleCloudJsonKeyfile, cancellationToken);
            variables.Set($"{account}.JsonKey", Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonKey)));
            variables.Set("Octopus.Action.GoogleCloud.Project", "gke-project");
            variables.Set("Octopus.Action.GoogleCloud.Region", "gke-region");
            variables.Set("Octopus.Action.GoogleCloud.Zone", "gke-zone");
            var wrapper = CreateWrapper();
            TestScriptInReadOnlyMode(wrapper).AssertSuccess();
        }

        [Test]
        [WindowsTest] // This test requires the GKE GCloud Auth plugin. Currently only configured to install on Windows
        public async Task ExecutionWithGoogleCloudAccount_WhenNeitherZoneOrRegionAreProvided()
        {
            variables.Set(Deployment.SpecialVariables.Account.AccountType, "GoogleCloudAccount");
            variables.Set(SpecialVariables.GkeClusterName, "gke-cluster-name");
            var account = "gke_account";
            variables.Set("Octopus.Action.GoogleCloudAccount.Variable", account);
            var jsonKey = await ExternalVariables.Get(ExternalVariable.GoogleCloudJsonKeyfile, cancellationToken);
            variables.Set($"{account}.JsonKey", Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonKey)));
            variables.Set("Octopus.Action.GoogleCloud.Project", "gke-project");
            var wrapper = CreateWrapper();
            TestScriptInReadOnlyMode(wrapper).AssertFailure();
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
                var wrapper = CreateWrapper();
                TestScriptInReadOnlyMode(wrapper, redactMap: new Dictionary<string, string>() { { tempExe.FilePath, "<customkubectl>" } }).AssertSuccess();
            }
        }

        KubernetesContextScriptWrapper CreateWrapper()
        {
            return new KubernetesContextScriptWrapper(variables, log, new AssemblyEmbeddedResources(), new TestCalamariPhysicalFileSystem());
        }

        void SetTestClusterVariables()
        {
            variables.Set(SpecialVariables.ClusterUrl, ServerUrl);
            variables.Set(SpecialVariables.Namespace, "calamari-testing");
        }

        CalamariResult TestScriptInReadOnlyMode(IScriptWrapper wrapper, [CallerMemberName] string testName = null, [CallerFilePath] string filePath = null, Dictionary<string, string> redactMap = null)
        {
            redactMap = redactMap ?? new Dictionary<string, string>();
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

                var configuration = AssentConfiguration.Default.UsingSanitiser(approval =>
                                                                               {
                                                                                   approval = approval.Replace("\r\n", "\n");

                                                                                   // On Windows machines the chmod message wont appear
                                                                                   // To avoid having multiple approval files, just fake the line.
                                                                                   // This specific logic will be explicitly checked elsewhere
                                                                                   if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                                                                                   {
                                                                                       const string chmodMsg = "[Verbose] \"chmod\" u=rw,g=,o= \"<path>kubectl-octo.yml\"";
                                                                                       const string tempPathMsg = "[Verbose] Temporary kubectl config set to <path>kubectl-octo.yml";
                                                                                       approval = approval.Replace(tempPathMsg, $"{chmodMsg}\n{tempPathMsg}");
                                                                                   }

                                                                                   return approval;
                                                                               });

                this.Assent(sb.ToString(), testName: testName, filePath: filePath, configuration: configuration);

                return output;
            }
        }

        CalamariResult ExecuteScriptInRecordOnlyMode(IScriptWrapper wrapper, string scriptName)
        {
            return ExecuteScriptInternal(new RecordOnly(variables, installTools), wrapper, scriptName);
        }

        CalamariResult ExecuteScriptInternal(ICommandLineRunner runner, IScriptWrapper wrapper, string scriptName)
        {
            var wrappers = new List<IScriptWrapper>(new[] { wrapper });
            if (variables.Get(Deployment.SpecialVariables.Account.AccountType) == "AmazonWebServicesAccount")
            {
                wrappers.Add(new AwsScriptWrapper(log, variables) { VerifyAmazonLogin = () => Task.FromResult(true) });
            }

            var engine = new ScriptEngine(wrappers, log, new DotnetScriptCompilationWarningOutputSink());
            var result = engine.Execute(new Script(scriptName), variables, runner, environmentVariables);

            return new CalamariResult(result.ExitCode, new CaptureCommandInvocationOutputSink());
        }

        async Task InstallTools(Func<InstallTools, Task> toolInstaller)
        {
            var tools = new InstallTools(TestContext.Progress.WriteLine);

            await toolInstaller(tools);

            installTools = tools;
        }

        static async Task InstallAwsCli(InstallTools tools)
        {
            await tools.InstallAwsCli();
        }

        async Task InstallGCloud(InstallTools tools)
        {
            await tools.InstallGCloud();

            environmentVariables.Add("USE_GKE_GCLOUD_AUTH_PLUGIN", "True");
        }

        class RecordOnly : ICommandLineRunner
        {
            IVariables Variables;
            readonly InstallTools installTools;

            public RecordOnly(IVariables variables, InstallTools installTools)
            {
                Variables = variables;
                this.installTools = installTools;
            }

            public CommandResult Execute(CommandLineInvocation invocation)
            {
                // If were running an aws command (we check the version and get the eks token endpoint) or checking it's location i.e. 'where aws' we want to run the actual command result.
                if (new[] { "aws", "aws.exe" }.Contains(invocation.Executable))
                {
                    ExecuteCommand(invocation, installTools.AwsCliExecutable);
                }

                if (new[] { "kubectl", "kubectl.exe" }.Contains(invocation.Executable) && invocation.Arguments.Contains("version --client --output=json"))
                {
                    ExecuteCommand(invocation, invocation.Executable);
                }

                // We only want to output executable string. eg. ExecuteCommandAndReturnOutput("where", "kubectl.exe")
                if (new[] { "kubectl", "az", "gcloud", "kubectl.exe", "az.cmd", "gcloud.cmd", "aws", "aws.exe", "aws-iam-authenticator", "aws-iam-authenticator.exe", "kubelogin", "kubelogin.exe", "gke-gcloud-auth-plugin", "gke-gcloud-auth-plugin.exe" }.Contains(invocation.Arguments))
                    invocation.AdditionalInvocationOutputSink?.WriteInfo(Path.GetFileNameWithoutExtension(invocation.Arguments));

                return new CommandResult(invocation.ToString(), 0);
            }

            void ExecuteCommand(CommandLineInvocation invocation, string executable)
            {
                var captureCommandOutput = new CaptureCommandInvocationOutputSink();
                var installedToolInvocation = new CommandLineInvocation(executable, invocation.Arguments)
                {
                    EnvironmentVars = invocation.EnvironmentVars,
                    WorkingDirectory = invocation.WorkingDirectory,
                    OutputAsVerbose = false,
                    OutputToLog = false,
                    AdditionalInvocationOutputSink = captureCommandOutput
                };

                var commandLineRunner = new CommandLineRunner(new SilentLog(), Variables);
                commandLineRunner.Execute(installedToolInvocation);
                foreach (var message in captureCommandOutput.AllMessages)
                {
                    invocation.AdditionalInvocationOutputSink?.WriteInfo(message);
                }
            }
        }
    }
}