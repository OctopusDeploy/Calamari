#if NETCORE
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Calamari.Aws.Kubernetes.Discovery;
using Calamari.Azure;
using Calamari.Common.Features.Discovery;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.ServiceMessages;
using Calamari.Kubernetes.Commands;
using Calamari.Testing;
using Calamari.Testing.Helpers;
using Calamari.Tests.AWS;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using SpecialVariables = Calamari.Kubernetes.SpecialVariables;

namespace Calamari.Tests.KubernetesFixtures
{
    [TestFixture]
    [Category(TestCategory.RunOnceOnWindowsAndLinux)]
    public class KubernetesContextScriptWrapperLiveFixture: KubernetesContextScriptWrapperLiveFixtureBase
    {
        const string KubeCtlExecutableVariableName = "Octopus.Action.Kubernetes.CustomKubectlExecutable";
        
        InstallTools installTools;

        string eksClientID;
        string eksSecretKey;
        string eksClusterEndpoint;
        string eksClusterCaCertificate;
        string eksClusterName;
        string awsVpcID;
        string awsSubnetID;
        string awsIamInstanceProfileName;
        string region;
        string eksClusterArn;
        string eksClusterUrl;
        string eksIamRolArn;
        
        string aksClusterHost;
        string aksClusterClientCertificate;
        string aksClusterClientKey;
        string aksClusterCaCertificate;
        string aksClusterName;
        string azurermResourceGroup;
        string aksPodServiceAccountToken;
        
        string gkeToken;
        string gkeProject;
        string gkeLocation;
        string gkeClusterCaCertificate;
        string gkeClusterEndpoint;
        string gkeClusterName;

        string terraformWorkingFolder;

        [OneTimeSetUp]
        public async Task SetupInfrastructure()
        {
            region = RegionRandomiser.GetARegion();
            await TestContext.Progress.WriteLineAsync($"Aws Region chosen: {region}");
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
            variables.Set(KubeCtlExecutableVariableName, installTools.KubectlExecutable);
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
            eksClusterArn = jsonOutput["eks_cluster_arn"]["value"].Value<string>();
            eksClusterUrl = jsonOutput["eks_cluster_url"]["value"].Value<string>();
            eksIamRolArn = jsonOutput["eks_iam_role_arn"]["value"].Value<string>();
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
                { "TF_VAR_test_namespace", TestNamespace },
                { "GOOGLE_CLOUD_KEYFILE_JSON", ExternalVariables.Get(ExternalVariable.GoogleCloudJsonKeyfile) }
            };
        
            var result = SilentProcessRunner.ExecuteCommand(installTools.TerraformExecutable,
                string.Join(" ", args.Concat(new[] { "-no-color" })),
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
                TestContext.Error.WriteLine);
        
            result.ExitCode.Should().Be(0);
        
            return sb.ToString().Trim(Environment.NewLine.ToCharArray());
        }

        string InitialiseTerraformWorkingFolder(string folderName, string filesSource)
        {
            var terraformWorkingFolder = Path.Combine(testFolder, folderName);
            if (Directory.Exists(terraformWorkingFolder))
                Directory.Delete(terraformWorkingFolder, true);
            
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
            
            // When authorising via AWS, We need to make sure we are using the correct version of
            // kubectl for the test script as newer versions may cause kubectl to fail with an error like:
            // 'error: exec plugin: invalid apiVersion "client.authentication.k8s.io/v1alpha1"'
            var kubectlExecutable = variables.Get(KubeCtlExecutableVariableName) ??
                throw new Exception($"Unable to find required kubectl executable in variable '{KubeCtlExecutableVariableName}'");
            
            TestScript(wrapper, "Test-Script", kubectlExecutable);
        }

        [Test]
        public void UsingEc2Instance()
        {
            var terraformWorkingFolder = InitialiseTerraformWorkingFolder("terraform_working_ec2", "KubernetesFixtures/Terraform/EC2");
        
            var env = new Dictionary<string, string>
            {
                { "TF_VAR_iam_role_arn", eksIamRolArn },
                { "TF_VAR_cluster_name", eksClusterName },
                { "TF_VAR_aws_vpc_id", awsVpcID },
                { "TF_VAR_aws_subnet_id", awsSubnetID },
                { "TF_VAR_aws_iam_instance_profile_name", awsIamInstanceProfileName },
                { "TF_VAR_aws_region", region }
            };
        
            RunTerraformInternal(terraformWorkingFolder, env, "init");
            try
            {
                // This actual tests are run via EC2/test.sh which executes the tests in
                // KubernetesContextScriptWrapperLiveFixtureForAmazon.cs
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
            var serviceMessageCollectorLog = new ServiceMessageCollectorLog();
            Log = serviceMessageCollectorLog;
        
            var scope = new TargetDiscoveryScope("TestSpace",
                "Staging",
                "testProject",
                null,
                new[] { "discovery-role" },
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
                    KubernetesAuthenticationContextTypes.AzureServicePrincipal,
                    "Accounts-1",
                    account);
        
            var targetDiscoveryContext =
                new TargetDiscoveryContext<AccountAuthenticationDetails<ServicePrincipalAccount>>(scope,
                    authenticationDetails);
        
            var result =
                ExecuteDiscoveryCommand(targetDiscoveryContext,
                    new[]{"Calamari.Azure"}
                );
            
            result.AssertSuccess();
        
            var expectedServiceMessage = new ServiceMessage(
                KubernetesDiscoveryCommand.CreateKubernetesTargetServiceMessageName,
                new Dictionary<string, string>
                {
                    { "name", aksClusterName },
                    { "clusterName", aksClusterName },
                    { "clusterResourceGroup", azurermResourceGroup },
                    { "skipTlsVerification", bool.TrueString },
                    { "octopusDefaultWorkerPoolIdOrName", scope.WorkerPoolId },
                    { "octopusAccountIdOrName", "Accounts-1" },
                    { "octopusRoles", "discovery-role" },
                    { "updateIfExisting", bool.TrueString },
                    { "isDynamic", bool.TrueString },
                    { "awsAssumeRole", bool.FalseString },
                });
        
            serviceMessageCollectorLog.ServiceMessages.Should()
                                      .ContainSingle(s => s.Properties["name"] == aksClusterName)
                                      .Which.Should()
                                      .BeEquivalentTo(expectedServiceMessage);
        }

        [Test]
        public void DiscoverKubernetesClusterWithEnvironmentVariableCredentialsAndIamRole()
        {
            const string accessKeyEnvVar = "AWS_ACCESS_KEY_ID";
            const string secretKeyEnvVar = "AWS_SECRET_ACCESS_KEY";
            var originalAccessKey = Environment.GetEnvironmentVariable(accessKeyEnvVar);
            var originalSecretKey = Environment.GetEnvironmentVariable(secretKeyEnvVar);

            try
            {
                Environment.SetEnvironmentVariable(accessKeyEnvVar, eksClientID);
                Environment.SetEnvironmentVariable(secretKeyEnvVar, eksSecretKey);
                
                var authenticationDetails = new AwsAuthenticationDetails
                {
                    Type = "Aws",
                    Credentials = new AwsCredentials { Type = "worker" },
                    Role = new AwsAssumedRole
                    {
                        Type = "assumeRole",
                        Arn = eksIamRolArn
                    },
                    Regions = new []{region}
                };
                
                var serviceMessageProperties = new Dictionary<string, string>
                    {
                        { "name", eksClusterArn },
                        { "clusterName", eksClusterName },
                        { "clusterUrl", eksClusterEndpoint },
                        { "skipTlsVerification", bool.TrueString },
                        { "octopusDefaultWorkerPoolIdOrName", "WorkerPools-1" },
                        { "octopusRoles", "discovery-role" },
                        { "updateIfExisting", bool.TrueString },
                        { "isDynamic", bool.TrueString },
                        { "awsAssumeRole", bool.TrueString },
                        { "awsAssumeRoleArn", eksIamRolArn },
                    };

                DoDiscoveryAndAssertReceivedServiceMessageWithMatchingProperties(authenticationDetails,
                    serviceMessageProperties);
            }
            finally
            {
                Environment.SetEnvironmentVariable(accessKeyEnvVar, originalAccessKey);
                Environment.SetEnvironmentVariable(secretKeyEnvVar, originalSecretKey);
            }
        }

        [Test]
        public void DiscoverKubernetesClusterWithEnvironmentVariableCredentialsAndNoIamRole()
        {
            const string accessKeyEnvVar = "AWS_ACCESS_KEY_ID";
            const string secretKeyEnvVar = "AWS_SECRET_ACCESS_KEY";
            var originalAccessKey = Environment.GetEnvironmentVariable(accessKeyEnvVar);
            var originalSecretKey = Environment.GetEnvironmentVariable(secretKeyEnvVar);

            try
            {
                Environment.SetEnvironmentVariable(accessKeyEnvVar, eksClientID);
                Environment.SetEnvironmentVariable(secretKeyEnvVar, eksSecretKey);
                
                var authenticationDetails = new AwsAuthenticationDetails
                {
                    Type = "Aws",
                    Credentials = new AwsCredentials { Type = "worker" },
                    Role = new AwsAssumedRole { Type = "noAssumedRole" },
                    Regions = new []{region}
                };
                
                var serviceMessageProperties = new Dictionary<string, string>
                    {
                        { "name", eksClusterArn },
                        { "clusterName", eksClusterName },
                        { "clusterUrl", eksClusterEndpoint },
                        { "skipTlsVerification", bool.TrueString },
                        { "octopusDefaultWorkerPoolIdOrName", "WorkerPools-1" },
                        { "octopusRoles", "discovery-role" },
                        { "updateIfExisting", bool.TrueString },
                        { "isDynamic", bool.TrueString },
                        { "awsAssumeRole", bool.FalseString }
                    };

                DoDiscoveryAndAssertReceivedServiceMessageWithMatchingProperties(authenticationDetails,
                    serviceMessageProperties);
            }
            finally
            {
                Environment.SetEnvironmentVariable(accessKeyEnvVar, originalAccessKey);
                Environment.SetEnvironmentVariable(secretKeyEnvVar, originalSecretKey);
            }
        }
        
        [Test]
        public void DiscoverKubernetesClusterWithAwsAccountCredentialsAndNoIamRole()
        {
            var authenticationDetails = new AwsAuthenticationDetails
            {
                Type = "Aws",
                Credentials = new AwsCredentials
                {
                    Account = new AwsAccount
                    {
                        AccessKey = eksClientID,
                        SecretKey = eksSecretKey
                    },
                    AccountId = "Accounts-1",
                    Type = "account"
                },
                Role = new AwsAssumedRole { Type = "noAssumedRole" },
                Regions = new []{region}
            };

            var serviceMessageProperties = new Dictionary<string, string>
            {
                { "name", eksClusterArn },
                { "clusterName", eksClusterName },
                { "clusterUrl", eksClusterEndpoint },
                { "skipTlsVerification", bool.TrueString },
                { "octopusDefaultWorkerPoolIdOrName", "WorkerPools-1" },
                { "octopusAccountIdOrName", "Accounts-1" },
                { "octopusRoles", "discovery-role" },
                { "updateIfExisting", bool.TrueString },
                { "isDynamic", bool.TrueString },
                { "awsAssumeRole", bool.FalseString }
            };
            
            DoDiscoveryAndAssertReceivedServiceMessageWithMatchingProperties(authenticationDetails, serviceMessageProperties);
        }
        
        [Test]
        public void DiscoverKubernetesClusterWithAwsAccountCredentialsAndIamRole()
        {
            const int sessionDuration = 900;
            var authenticationDetails = new AwsAuthenticationDetails
            {
                Type = "Aws",
                Credentials = new AwsCredentials
                {
                    Account = new AwsAccount
                    {
                        AccessKey = eksClientID,
                        SecretKey = eksSecretKey
                    },
                    AccountId = "Accounts-1",
                    Type = "account"
                },
                Role = new AwsAssumedRole
                {
                    Type = "assumeRole",
                    Arn = eksIamRolArn,
                    SessionName = "ThisIsASessionName",
                    SessionDuration = sessionDuration
                },
                Regions = new []{region}
            };

            var serviceMessageProperties = new Dictionary<string, string>
            {
                { "name", eksClusterArn },
                { "clusterName", eksClusterName },
                { "clusterUrl", eksClusterEndpoint },
                { "skipTlsVerification", bool.TrueString },
                { "octopusDefaultWorkerPoolIdOrName", "WorkerPools-1" },
                { "octopusAccountIdOrName", "Accounts-1" },
                { "octopusRoles", "discovery-role" },
                { "updateIfExisting", bool.TrueString },
                { "isDynamic", bool.TrueString },
                { "awsAssumeRole", bool.TrueString },
                { "awsAssumeRoleArn", eksIamRolArn },
                { "awsAssumeRoleSession", "ThisIsASessionName" },
                { "awsAssumeRoleSessionDurationSeconds", sessionDuration.ToString() }
            };
            
            DoDiscoveryAndAssertReceivedServiceMessageWithMatchingProperties(authenticationDetails, serviceMessageProperties);
        }
        
        [Test]
        public void DiscoverKubernetesClusterWithNoValidCredentials()
        {
            const string accessKeyEnvVar = "AWS_ACCESS_KEY_ID";
            const string secretKeyEnvVar = "AWS_SECRET_ACCESS_KEY";
            var originalAccessKey = Environment.GetEnvironmentVariable(accessKeyEnvVar);
            var originalSecretKey = Environment.GetEnvironmentVariable(secretKeyEnvVar);

            try
            {
                Environment.SetEnvironmentVariable(accessKeyEnvVar, null);
                Environment.SetEnvironmentVariable(secretKeyEnvVar, null);
                
                var authenticationDetails = new AwsAuthenticationDetails
                {
                    Type = "Aws",
                    Credentials = new AwsCredentials { Type = "worker" },
                    Role = new AwsAssumedRole { Type = "noAssumedRole" },
                    Regions = new []{region}
                };
                
                var serviceMessageCollectorLog = new ServiceMessageCollectorLog();
                Log = serviceMessageCollectorLog;
                
                DoDiscovery(authenticationDetails);

                serviceMessageCollectorLog.ServiceMessages.Should().BeEmpty();

                serviceMessageCollectorLog.Messages.Should().NotContain(m => m.Level == InMemoryLog.Level.Error);

                serviceMessageCollectorLog.StandardError.Should().BeEmpty();

                serviceMessageCollectorLog.Messages.Should()
                                          .ContainSingle(m =>
                                              m.Level == InMemoryLog.Level.Warn &&
                                              m.FormattedMessage ==
                                              "Unable to authorise credentials, see verbose log for details.");
            }
            finally
            {
                Environment.SetEnvironmentVariable(accessKeyEnvVar, originalAccessKey);
                Environment.SetEnvironmentVariable(secretKeyEnvVar, originalSecretKey);
            }
        }
        
        [Test]
        public void DiscoverKubernetesClusterWithInvalidAccountCredentials()
        {
            var authenticationDetails = new AwsAuthenticationDetails
            {
                Type = "Aws",
                Credentials = new AwsCredentials
                {
                    Account = new AwsAccount
                    {
                        AccessKey = "abcdefg",
                        SecretKey = null
                    },
                    AccountId = "Accounts-1",
                    Type = "account"
                },
                Role = new AwsAssumedRole { Type = "noAssumedRole" },
                Regions = new []{region}
            };
            
            var serviceMessageCollectorLog = new ServiceMessageCollectorLog();
            Log = serviceMessageCollectorLog;
            
            DoDiscovery(authenticationDetails);

            serviceMessageCollectorLog.ServiceMessages.Should().BeEmpty();

            serviceMessageCollectorLog.Messages.Should().NotContain(m => m.Level == InMemoryLog.Level.Error);

            serviceMessageCollectorLog.StandardError.Should().BeEmpty();

            serviceMessageCollectorLog.Messages.Should()
                                      .ContainSingle(m =>
                                          m.Level == InMemoryLog.Level.Warn &&
                                          m.FormattedMessage ==
                                          "Unable to authorise credentials, see verbose log for details."); 
        }
    }
}
#endif