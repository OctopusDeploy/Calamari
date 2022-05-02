#if NETCORE
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Calamari.CloudAccounts.Azure;
using Calamari.Common.Features.Discovery;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.ServiceMessages;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.Commands;
using Calamari.Kubernetes.Commands.Discovery;
using Calamari.Testing;
using Calamari.Testing.Helpers;
using Calamari.Tests.AWS;
using Calamari.Tests.Helpers;
using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using SpecialVariables = Calamari.Kubernetes.SpecialVariables;

namespace Calamari.Tests.KubernetesFixtures
{
    [TestFixture]
    [Category(TestCategory.RunOnceOnWindowsAndLinux)]
    public class KubernetesContextScriptWrapperLiveFixture: KubernetesContextScriptWrapperLiveFixtureBase
    {
        InstallTools installTools;

        string eksClientID;
        string eksSecretKey;
        string eksClusterEndpoint;
        string eksClusterCaCertificate;
        string aksClusterHost;
        string aksClusterClientCertificate;
        string aksClusterClientKey;
        string aksClusterCaCertificate;
        string gkeToken;
        string gkeProject;
        string gkeLocation;
        string gkeClusterCaCertificate;
        string gkeClusterEndpoint;
        string gkeClusterName;
        string eksClusterName;
        string aksClusterName;
        string azurermResourceGroup;
        string aksPodServiceAccountToken;
        string terraformWorkingFolder;
        string awsVpcID;
        string awsSubnetID;
        string awsIamInstanceProfileName;
        string region;

        ServiceMessageCollectorLog serviceMessageCollectorLog;

        [OneTimeSetUp]
        public async Task SetupInfrastructure()
        {
            region = RegionRandomiser.GetARegion();
            terraformWorkingFolder = InitialiseTerraformWorkingFolder("terraform_working", "KubernetesFixtures/Terraform/Clusters");

            installTools = new InstallTools(TestContext.Progress.WriteLine);
            await installTools.Install();

            InitialiseInfrastructure(terraformWorkingFolder);
        }

        [OneTimeTearDown]
        public void TearDownInfrastructure()
        {
            RunTerraformDestroy(terraformWorkingFolder);
        }

        [SetUp]
        public void SetExtraVariables()
        {
            serviceMessageCollectorLog = new ServiceMessageCollectorLog();
            variables.Set("Octopus.Action.Kubernetes.CustomKubectlExecutable", installTools.KubectlExecutable);
        }

        protected override Dictionary<string, string> GetEnvironments()
        {
            var currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            var delimiter = CalamariEnvironment.IsRunningOnWindows ? ";" : ":";
            if (currentPath.Length > 0 && !currentPath.EndsWith(delimiter))
            {
                currentPath += delimiter;
            }
            currentPath += Path.GetDirectoryName(installTools.AwsAuthenticatorExecutable);
            currentPath += delimiter;
            currentPath += Path.GetDirectoryName(installTools.GcloudExecutable);

            return new Dictionary<string, string> { { "PATH", currentPath } };
        }

        void InitialiseInfrastructure(string terraformWorkingFolder)
        {
            RunTerraformInternal(terraformWorkingFolder, "init");
            RunTerraformInternal(terraformWorkingFolder, "apply", "-auto-approve");
            var jsonOutput = JObject.Parse(RunTerraformOutput(terraformWorkingFolder));

            eksClientID = jsonOutput["eks_client_id"]["value"].Value<string>();
            eksSecretKey = jsonOutput["eks_secret_key"]["value"].Value<string>();
            eksClusterEndpoint = jsonOutput["eks_cluster_endpoint"]["value"].Value<string>();
            eksClusterCaCertificate = jsonOutput["eks_cluster_ca_certificate"]["value"].Value<string>();
            eksClusterName = jsonOutput["eks_cluster_name"]["value"].Value<string>();
            awsVpcID = jsonOutput["aws_vpc_id"]["value"].Value<string>();
            awsSubnetID = jsonOutput["aws_subnet_id"]["value"].Value<string>();
            awsIamInstanceProfileName = jsonOutput["aws_iam_instance_profile_name"]["value"].Value<string>();

            aksClusterHost = jsonOutput["aks_cluster_host"]["value"].Value<string>();
            aksClusterClientCertificate = jsonOutput["aks_cluster_client_certificate"]["value"].Value<string>();
            aksClusterClientKey = jsonOutput["aks_cluster_client_key"]["value"].Value<string>();
            aksClusterCaCertificate = jsonOutput["aks_cluster_ca_certificate"]["value"].Value<string>();
            aksClusterName = jsonOutput["aks_cluster_name"]["value"].Value<string>();
            aksPodServiceAccountToken = jsonOutput["aks_service_account_token"]["value"].Value<string>();
            azurermResourceGroup = jsonOutput["aks_rg_name"]["value"].Value<string>();

            gkeClusterEndpoint = jsonOutput["gke_cluster_endpoint"]["value"].Value<string>();
            gkeClusterCaCertificate = jsonOutput["gke_cluster_ca_certificate"]["value"].Value<string>();
            gkeToken = jsonOutput["gke_token"]["value"].Value<string>();
            gkeClusterName = jsonOutput["gke_cluster_name"]["value"].Value<string>();
            gkeProject = jsonOutput["gke_cluster_project"]["value"].Value<string>();
            gkeLocation = jsonOutput["gke_cluster_location"]["value"].Value<string>();
        }

        void RunTerraformDestroy(string terraformWorkingFolder, Dictionary<string, string> env = null)
        {
            RunTerraformInternal(terraformWorkingFolder, env ?? new Dictionary<string, string>(), "destroy", "-auto-approve");
        }

        string RunTerraformOutput(string terraformWorkingFolder)
        {
            return RunTerraformInternal(terraformWorkingFolder, new Dictionary<string, string>(), false, "output", "-json");
        }

        string RunTerraformInternal(string terraformWorkingFolder, params string[] args)
        {
            return RunTerraformInternal(terraformWorkingFolder, new Dictionary<string, string>(), args);
        }

        string RunTerraformInternal(string terraformWorkingFolder, Dictionary<string, string> env, params string[] args)
        {
            return RunTerraformInternal(terraformWorkingFolder, env, true, args);
        }

        string RunTerraformInternal(string terraformWorkingFolder, Dictionary<string, string> env, bool printOut, params string[] args)
        {
            var sb = new StringBuilder();
            var environmentVars = new Dictionary<string, string>(env)
            {
                { "TF_IN_AUTOMATION ", Boolean.TrueString },
                { "AWS_ACCESS_KEY_ID", ExternalVariables.Get(ExternalVariable.AwsCloudFormationAndS3AccessKey) },
                { "AWS_SECRET_ACCESS_KEY", ExternalVariables.Get(ExternalVariable.AwsCloudFormationAndS3SecretKey) },
                { "AWS_DEFAULT_REGION", region },
                { "ARM_SUBSCRIPTION_ID", ExternalVariables.Get(ExternalVariable.AzureSubscriptionId) },
                { "ARM_CLIENT_ID", ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId) },
                { "ARM_CLIENT_SECRET", ExternalVariables.Get(ExternalVariable.AzureSubscriptionPassword) },
                { "ARM_TENANT_ID", ExternalVariables.Get(ExternalVariable.AzureSubscriptionTenantId) },
                { "TF_VAR_aks_client_id", ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId) },
                { "TF_VAR_aks_client_secret", ExternalVariables.Get(ExternalVariable.AzureSubscriptionPassword) },
                { "TF_VAR_tests_source_dir", testFolder },
                { "TF_VAR_test_namespace", testNamespace },
                { "GOOGLE_CLOUD_KEYFILE_JSON", ExternalVariables.Get(ExternalVariable.GoogleCloudJsonKeyfile) }
            };

            var result = SilentProcessRunner.ExecuteCommand(installTools.TerraformExecutable,
                                                            string.Join(" ", args.Concat(new [] {"-no-color"})),
                                                            terraformWorkingFolder,
                                                            environmentVars,
                                                            s =>
                                                            {
                                                                sb.AppendLine(s);
                                                                if (printOut)
                                                                {
                                                                    TestContext.Progress.WriteLine(s);
                                                                }
                                                            },
                                                            Console.Error.WriteLine);

            result.ExitCode.Should().Be(0);

            return sb.ToString().Trim(Environment.NewLine.ToCharArray());
        }

        string InitialiseTerraformWorkingFolder(string folderName, string filesSource)
        {
            var terraformWorkingFolder = Path.Combine(testFolder, folderName);
            Directory.CreateDirectory(terraformWorkingFolder);

            foreach (var file in Directory.EnumerateFiles(Path.Combine(testFolder, filesSource)))
            {
                File.Copy(file, Path.Combine(terraformWorkingFolder, Path.GetFileName(file)), true);
            }

            return terraformWorkingFolder;
        }

        [Test]
        public void AuthorisingWithToken()
        {
            variables.Set(SpecialVariables.ClusterUrl, $"https://{gkeClusterEndpoint}");
            variables.Set(Deployment.SpecialVariables.Account.AccountType, "Token");
            variables.Set(Deployment.SpecialVariables.Account.Token, gkeToken);
            var certificateAuthority = "myauthority";
            variables.Set("Octopus.Action.Kubernetes.CertificateAuthority", certificateAuthority);
            variables.Set($"{certificateAuthority}.CertificatePem", gkeClusterCaCertificate);
            var wrapper = CreateWrapper();
            TestScript(wrapper, "Test-Script");
        }

        [Test]
        public void AuthorisingWithPodServiceAccountToken()
        {
            variables.Set(SpecialVariables.ClusterUrl, aksClusterHost);
            using (var dir = TemporaryDirectory.Create())
            using (var podServiceAccountToken = new TemporaryFile(Path.Combine(dir.DirectoryPath, "podServiceAccountToken")))
            using (var certificateAuthority = new TemporaryFile(Path.Combine(dir.DirectoryPath, "certificateAuthority")))
            {
                File.WriteAllText(podServiceAccountToken.FilePath, aksPodServiceAccountToken);
                File.WriteAllText(certificateAuthority.FilePath, aksClusterCaCertificate);
                variables.Set("Octopus.Action.Kubernetes.PodServiceAccountTokenPath", podServiceAccountToken.FilePath);
                variables.Set("Octopus.Action.Kubernetes.CertificateAuthorityPath", certificateAuthority.FilePath);
                var wrapper = CreateWrapper();
                TestScript(wrapper, "Test-Script");
            }
        }

        [Test]
        public void AuthorisingWithAzureServicePrincipal()
        {
            variables.Set(Deployment.SpecialVariables.Account.AccountType, "AzureServicePrincipal");
            variables.Set("Octopus.Action.Kubernetes.AksClusterResourceGroup", azurermResourceGroup);
            variables.Set(SpecialVariables.AksClusterName, aksClusterName);
            variables.Set("Octopus.Action.Kubernetes.AksAdminLogin", Boolean.FalseString);
            variables.Set("Octopus.Action.Azure.SubscriptionId", ExternalVariables.Get(ExternalVariable.AzureSubscriptionId));
            variables.Set("Octopus.Action.Azure.TenantId", ExternalVariables.Get(ExternalVariable.AzureSubscriptionTenantId));
            variables.Set("Octopus.Action.Azure.Password", ExternalVariables.Get(ExternalVariable.AzureSubscriptionPassword));
            variables.Set("Octopus.Action.Azure.ClientId", ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId));
            var wrapper = CreateWrapper();
            TestScript(wrapper, "Test-Script");
        }

        [Test]
        public void AuthorisingWithGoogleCloudAccount()
        {
            variables.Set(Deployment.SpecialVariables.Account.AccountType, "GoogleCloudAccount");
            variables.Set(SpecialVariables.GkeClusterName, gkeClusterName);
            var account = "gke_account";
            variables.Set("Octopus.Action.GoogleCloudAccount.Variable", account);
            var jsonKey = ExternalVariables.Get(ExternalVariable.GoogleCloudJsonKeyfile);
            variables.Set($"{account}.JsonKey", Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonKey)));
            variables.Set("Octopus.Action.GoogleCloud.Project", gkeProject);
            variables.Set("Octopus.Action.GoogleCloud.Zone", gkeLocation);
            var wrapper = CreateWrapper();
            TestScript(wrapper, "Test-Script");
        }

        [Test]
        public void AuthorisingWithAmazonAccount()
        {
            variables.Set(Deployment.SpecialVariables.Account.AccountType, "AmazonWebServicesAccount");
            variables.Set(SpecialVariables.ClusterUrl, eksClusterEndpoint);
            variables.Set(SpecialVariables.EksClusterName, eksClusterName);
            variables.Set("Octopus.Action.Aws.Region", region);
            var account = "eks_account";
            variables.Set("Octopus.Action.AwsAccount.Variable", account);
            variables.Set($"{account}.AccessKey", eksClientID);
            variables.Set($"{account}.SecretKey", eksSecretKey);
            var certificateAuthority = "myauthority";
            variables.Set("Octopus.Action.Kubernetes.CertificateAuthority", certificateAuthority);
            variables.Set($"{certificateAuthority}.CertificatePem", eksClusterCaCertificate);
            var wrapper = CreateWrapper();
            TestScript(wrapper, "Test-Script");
        }

        [Test]
        public void UsingEc2Instance()
        {
            var terraformWorkingFolder = InitialiseTerraformWorkingFolder("terraform_working_ec2", "KubernetesFixtures/Terraform/EC2");

            var env = new Dictionary<string, string>
            {
                { "TF_VAR_cluster_name", eksClusterName },
                { "TF_VAR_aws_vpc_id", awsVpcID },
                { "TF_VAR_aws_subnet_id", awsSubnetID },
                { "TF_VAR_aws_iam_instance_profile_name", awsIamInstanceProfileName },
                { "TF_VAR_aws_region", region }
            };

            RunTerraformInternal(terraformWorkingFolder, env, "init");
            try
            {
                RunTerraformInternal(terraformWorkingFolder, env, "apply", "-auto-approve");
            }
            finally
            {
                RunTerraformDestroy(terraformWorkingFolder, env);
            }
        }

        [Test]
        public void AuthorisingWithClientCertificate()
        {
            variables.Set(SpecialVariables.ClusterUrl, aksClusterHost);
            var certificateAuthority = "myauthority";
            variables.Set("Octopus.Action.Kubernetes.CertificateAuthority", certificateAuthority);
            variables.Set($"{certificateAuthority}.CertificatePem", aksClusterCaCertificate);
            var clientCert = "myclientcert";
            variables.Set("Octopus.Action.Kubernetes.ClientCertificate", clientCert);
            variables.Set($"{clientCert}.CertificatePem", aksClusterClientCertificate);
            variables.Set($"{clientCert}.PrivateKeyPem", aksClusterClientKey);
            var wrapper = CreateWrapper();
            TestScript(wrapper, "Test-Script");
        }
        
        [Test]
        public void DiscoverKubernetesClusterWithAzureServicePrincipalAccount()
        {
            const string feedId = "Feeds-1";
            const string healthCheckContainerName = "MyHealthCheckContainerImage";
            
            var scope = new TargetDiscoveryScope("TestSpace",
                "Staging",
                "testProject",
                null,
                new[] { "eks-discovery-role" },
                "WorkerPool-1");

            var account = new ServicePrincipalAccount(
                ExternalVariables.Get(ExternalVariable.AzureSubscriptionId),
                ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId),
                ExternalVariables.Get(ExternalVariable.AzureSubscriptionTenantId),
                ExternalVariables.Get(ExternalVariable.AzureSubscriptionPassword),
                null,
                null,
                null);

            var authenticationDetails =
                new AccountAuthenticationDetails<ServicePrincipalAccount>(
                    AzureKubernetesDiscoverer.AuthenticationContextTypeName,
                    "Accounts-1",
                    account);

            var targetDiscoveryContext =
                new TargetDiscoveryContext<AccountAuthenticationDetails<ServicePrincipalAccount>>(scope,
                    authenticationDetails);

            var result =
                ExecuteDiscoveryCommand(targetDiscoveryContext,
                    ("Octopus.Kubernetes.HealthCheckContainer.FeedIdOrName", feedId),
                    ("Octopus.Kubernetes.HealthCheckContainer.Image", healthCheckContainerName)
                );
            
            result.AssertSuccess();

            var expectedServiceMessage = new ServiceMessage(
                KubernetesDiscoveryCommand.CreateKubernetesTargetServiceMessageName,
                new Dictionary<string, string>
                {
                    { "name", aksClusterName },
                    { "clusterName", aksClusterName },
                    { "clusterUrl", "" },
                    { "clusterResourceGroup", azurermResourceGroup },
                    { "clusterAdminLogin", "False" },
                    { "namespace", "" },
                    { "skipTlsVerification", "" },
                    { "octopusAccountIdOrName", "Accounts-1" },
                    { "octopusClientCertificateIdOrName", "" },
                    { "octopusServerCertificateIdOrName", "" },
                    { "octopusRoles", "eks-discovery-role" },
                    { "octopusDefaultWorkerPoolIdOrName", scope.WorkerPoolId },
                    { "healthCheckContainerImageFeedIdOrName", feedId},
                    { "healthCheckContainerImage", healthCheckContainerName},
                    { "updateIfExisting", "True" },
                    { "isDynamic", "True" },
                    { "clusterProject", "" },
                    { "clusterRegion", "" },
                    { "clusterZone", "" },
                    { "clusterImpersonateServiceAccount", "False" },
                    { "clusterServiceAccountEmails", "" },
                    { "clusterUseVmServiceAccount", "False" },
                });

            serviceMessageCollectorLog.ServiceMessages.Should()
                                      .ContainSingle()
                                      .Which.Should()
                                      .BeEquivalentTo(expectedServiceMessage);
        }
        
        CalamariResult ExecuteDiscoveryCommand<TAuthenticationDetails>(
            TargetDiscoveryContext<TAuthenticationDetails> discoveryContext,
            params (string key, string value)[] otherVariables)
            where TAuthenticationDetails : ITargetDiscoveryAuthenticationDetails
        {
            using var variablesFile = new TemporaryFile(Path.GetTempFileName());
            variables.Add(KubernetesDiscoveryCommand.ContextVariableName, JsonConvert.SerializeObject(discoveryContext));
            foreach (var (key, value) in otherVariables)
                variables.Add(key, value);
                
            variables.Save(variablesFile.FilePath);

            return InvokeInProcess(Calamari()
                                   .Action(KubernetesDiscoveryCommand.Name)
                                   .Argument("variables", variablesFile.FilePath));
        }

        class ServiceMessageCollectorLog : InMemoryLog
        {
            public List<ServiceMessage> ServiceMessages { get; } = new List<ServiceMessage>();
            public override void WriteServiceMessage(ServiceMessage serviceMessage)
            {
                ServiceMessages.Add(serviceMessage);
                
                base.WriteServiceMessage(serviceMessage);
            }
        }
    }
}
#endif