#if NETCORE
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Kubernetes;
using Calamari.Tests.Helpers;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

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
        string gkeClusterCaCertificate;
        string gkeClusterEndpoint;
        string eksClusterName;
        string aksClusterName;
        string azurermResourceGroup;
        string aksPodServiceAccountToken;
        string terraformWorkingFolder;
        string awsVpcID;
        string awsSubnetID;
        string awsIamInstanceProfileName;

        [OneTimeSetUp]
        public async Task SetupInfrastructure()
        {
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
                { "AWS_ACCESS_KEY_ID", Environment.GetEnvironmentVariable("AWS_E2E_AccessKeyId") },
                { "AWS_SECRET_ACCESS_KEY", Environment.GetEnvironmentVariable("AWS_E2E_SecretKeyId") },
                { "ARM_SUBSCRIPTION_ID", Environment.GetEnvironmentVariable("Azure_OctopusAPITester_SubscriptionId") },
                { "ARM_CLIENT_ID", Environment.GetEnvironmentVariable("Azure_OctopusAPITester_ClientId") },
                { "ARM_CLIENT_SECRET", Environment.GetEnvironmentVariable("Azure_OctopusAPITester_Password") },
                { "ARM_TENANT_ID", Environment.GetEnvironmentVariable("Azure_OctopusAPITester_TenantId") },
                { "TF_VAR_aks_client_id", Environment.GetEnvironmentVariable("Azure_OctopusAPITester_ClientId") },
                { "TF_VAR_aks_client_secret", Environment.GetEnvironmentVariable("Azure_OctopusAPITester_Password") },
                { "TF_VAR_tests_source_dir", testFolder },
                { "TF_VAR_test_namespace", testNamespace },
                { "GOOGLE_CLOUD_KEYFILE_JSON", Environment.GetEnvironmentVariable("GOOGLECLOUD_OCTOPUSAPITESTER_JSONKEY") }
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
            variables.Set("Octopus.Action.Azure.SubscriptionId", Environment.GetEnvironmentVariable("Azure_OctopusAPITester_SubscriptionId"));
            variables.Set("Octopus.Action.Azure.TenantId", Environment.GetEnvironmentVariable("Azure_OctopusAPITester_TenantId"));
            variables.Set("Octopus.Action.Azure.Password", Environment.GetEnvironmentVariable("Azure_OctopusAPITester_Password"));
            variables.Set("Octopus.Action.Azure.ClientId", Environment.GetEnvironmentVariable("Azure_OctopusAPITester_ClientId"));
            var wrapper = CreateWrapper();
            TestScript(wrapper, "Test-Script");
        }

        [Test]
        public void AuthorisingWithAmazonAccount()
        {
            variables.Set(Deployment.SpecialVariables.Account.AccountType, "AmazonWebServicesAccount");
            variables.Set(SpecialVariables.ClusterUrl, eksClusterEndpoint);
            variables.Set(SpecialVariables.EksClusterName, eksClusterName);
            variables.Set("Octopus.Action.Aws.Region", "ap-southeast-2");
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
                { "TF_VAR_aws_iam_instance_profile_name", awsIamInstanceProfileName }
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
    }
}
#endif