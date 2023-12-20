using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Calamari.Common.Features.Processes;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;
using Calamari.Kubernetes.Authentication;
using Calamari.Kubernetes.Integration;
using Calamari.Tests.Helpers;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.Authentication
{
    [TestFixture]
    public class SetupKubectlAuthenticationFixture : BaseSetupKubectlAuthenticationFixture
    {
        private const string ClusterUrl = "https://my-cool-cluster.com";
        private const string ClientCert = "my-cool-client-cert";
        private const string ClientCertPem = "my-cool-client-cert-pem";
        private const string ClientCertKey = "my-cool-client-cert-key";
        private const string CertificateAuthority = "my-cool-certificate-authority";
        private const string ServerCertPem = "my-cool-server-cert-pem";

        private const string AksClusterName = "my-cool-aks-cluster-name";
        private const string AksClusterResourceGroup = "my-cool-aks-cluster-resource-group";
        private const string AzureEnvironment = "my-cool-azure-environment";
        private const string AzurePassword = "my-cool-azure-password";
        private const string AzureJwt = "my-cool-azure-jwt";
        private const string AzureClientId = "my-cool-azure-client-id";
        private const string AzureTenantId = "my-cool-azure-tenant-id";
        private const string AzureSubscriptionId = "my-cool-azure-subscription-id";

        private const string GoogleAccountServiceAccountEmails = "my-cool-google-account-server-account-emails";
        private const string GoogleAccountJsonKey = "my-cool-google-account-json-key";
        private const string GoogleCloudAccountVariable = "my-cool-google-cloud-account-variable";
        private const string GoogleCloudZone = "my-cool-google-cloud-zone";
        private const string GoogleCloudRegion = "my-cool-google-cloud-region";
        private const string GoogleCloudProject = "my-cool-google-cloud-project";
        private const string GkeClusterName = "my-cool-gke-cluster-name";

        private const string CertificateAuthorityPath = "/path/to/certificate.authority";
        private const string PodServiceAccountTokenPath = "/path/to/pod-service-account.token";
        private const string PodServiceAccountToken = "my-cool-pod-server-account-token";


        [SetUp]
        public void Setup()
        {
            invocations.AddLogMessageFor("which", "gcloud", "gcloud");
            invocations.AddLogMessageFor("where", "gcloud.cmd", "gcloud");

            variables.AddFeatureToggles(FeatureToggle.KubernetesAksKubeloginFeatureToggle);
        }

        SetupKubectlAuthentication CreateSut() =>
            new SetupKubectlAuthentication(variables, log, commandLineRunner, kubectl, fileSystem, environmentVars, workingDirectory);

        [Test]
        public void Execute_WhenTrySetKubectlFails_Fails()
        {
            kubectl.When(s => s.SetKubectl())
                   .Do(x => throw new KubectlException("Error"));

        var sut = CreateSut();

            var result = sut.Execute();

            result.ExitCode.Should().NotBe(0);
        }

        [Test]
        public void Execute_WithNoClusterUrl_Fails()
        {
            var sut = CreateSut();

            var result = sut.Execute();

            result.ExitCode.Should().Be(1);

            log.Received().Error("Unable to configure Kubernetes authentication context. Please verify your target configuration.");
        }

        [TestCase(true, false, false, "Kubernetes client certificate does not include the certificate data")]
        [TestCase(false, true, false, "Kubernetes client certificate does not include the private key data")]
        [TestCase(false, false, true, "Kubernetes server certificate does not include the certificate data")]
        public void Execute_WithMissingCertificateData_Fails(bool missingClientPem, bool missingClientKey, bool missingServerPem, string errorMessage)
        {
            variables.Set(SpecialVariables.ClusterUrl, ClusterUrl);
            variables.Set(SpecialVariables.ClientCertificate, ClientCert);
            if(!missingClientPem) variables.Set(SpecialVariables.CertificatePem(ClientCert), ClientCertPem);
            if (!missingClientKey) variables.Set(SpecialVariables.PrivateKeyPem(ClientCert), ClientCertKey);
            variables.Set(SpecialVariables.CertificateAuthority, CertificateAuthority);
            if (!missingServerPem) variables.Set(SpecialVariables.CertificatePem(CertificateAuthority), ServerCertPem);

            var sut = CreateSut();

            var result = sut.Execute();

            result.ExitCode.Should().NotBe(0);

            log.Received().Error(errorMessage);
        }

        [Test]
        public void Execute_WhereNamespaceDoesNotExist_NamespaceCreated()
        {
            variables.Set(SpecialVariables.ClusterUrl, ClusterUrl);
            variables.Set(SpecialVariables.Namespace, Namespace);
            variables.Set(Deployment.SpecialVariables.Account.AccountType, AccountTypes.UsernamePassword);
            invocations.FailFor("kubectl", $"get namespace {Namespace}");

            var sut = CreateSut();

            var result = sut.Execute();

            result.VerifySuccess();

            invocations.TakeLast(2).Should().BeEquivalentTo(new[]
            {
                ("kubectl", $"get namespace {Namespace}"),
                ("kubectl", $"create namespace {Namespace}"),
            }, opts => opts.WithStrictOrdering());
        }

        [Test]
        public void Execute_WhereNamespaceCannotBeFoundOrCreated_LoggedButStillSucceeds()
        {
            variables.Set(SpecialVariables.ClusterUrl, ClusterUrl);
            variables.Set(SpecialVariables.Namespace, Namespace);
            invocations.FailFor("kubectl", $"get namespace {Namespace}");
            invocations.FailFor("kubectl", $"create namespace {Namespace}");
            variables.Set(Deployment.SpecialVariables.Account.AccountType, AccountTypes.UsernamePassword);
            var sut = CreateSut();

            var result = sut.Execute();

            result.VerifySuccess();

            invocations.TakeLast(2).Should().BeEquivalentTo(new[]
            {
                ("kubectl", $"get namespace {Namespace}"),
                ("kubectl", $"create namespace {Namespace}"),
            }, opts => opts.WithStrictOrdering());

            log.Received().Verbose("Could not create namespace. Continuing on, as it may not be working directly with the target.");
        }

        [Test]
        public void Execute_WithOutputKubeConfigFlagOn_GetsKubeConfig()
        {
            variables.Set(SpecialVariables.ClusterUrl, ClusterUrl);
            variables.Set(SpecialVariables.Namespace, Namespace);
            variables.Set(Deployment.SpecialVariables.Account.AccountType, AccountTypes.UsernamePassword);
            variables.AddFlag(SpecialVariables.OutputKubeConfig, true);

            var sut = CreateSut();

            var result = sut.Execute();

            result.VerifySuccess();

            invocations.TakeLast(1).Should().BeEquivalentTo(new[]
            {
                ("kubectl", "config view"),
            });
        }

        [Test]
        public void Execute_WithToken_ConfiguresAuthenticationCorrectly()
        {
            const string token = "my-cool-token";

            variables.Set(SpecialVariables.ClusterUrl, ClusterUrl);
            variables.Set(SpecialVariables.Namespace, Namespace);
            variables.Set(Deployment.SpecialVariables.Account.Token, token);
            variables.Set(Deployment.SpecialVariables.Account.AccountType, AccountTypes.Token);
            var sut = CreateSut();

            var result = sut.Execute();

            result.VerifySuccess();

            var expected = new[]
            {
                ("kubectl", $"config set-cluster octocluster --server={ClusterUrl}"),
                ("kubectl", $"config set-context octocontext --user=octouser --cluster=octocluster --namespace={Namespace}"),
                ("kubectl", "config use-context octocontext"),
                ("kubectl", $"config set-credentials octouser --token={token}"),
                ("kubectl", $"get namespace {Namespace}")
            };
            invocations.Should().BeEquivalentTo(expected, opts => opts.WithStrictOrdering());
        }

        [Test]
        public void Execute_WithTokenButTokenVariableMissing_Fails()
        {
            variables.Set(SpecialVariables.ClusterUrl, ClusterUrl);
            variables.Set(SpecialVariables.Namespace, Namespace);
            variables.Set(Deployment.SpecialVariables.Account.AccountType, AccountTypes.Token);
            var sut = CreateSut();

            var result = sut.Execute();

            result.ExitCode.Should().NotBe(0);

            log.Received().Error("Kubernetes authentication Token is missing");
        }

        [TestCase(AccountTypes.AzureServicePrincipal, true)]
        [TestCase(AccountTypes.AzureServicePrincipal, false)]
        [TestCase(AccountTypes.AzureOidc, true)]
        [TestCase(AccountTypes.AzureOidc, false)]
        public void Execute_WithAzureAccountType_ConfiguresAuthenticationCorrectly(string accountType, bool withJwt)
        {
            invocations.AddLogMessageFor("which", "az", "az");
            invocations.AddLogMessageFor("where", "az.cmd", "az");

            variables.Set(SpecialVariables.Namespace, Namespace);

            variables.Set(Deployment.SpecialVariables.Action.Azure.Environment, AzureEnvironment);
            variables.Set(Deployment.SpecialVariables.Action.Azure.SubscriptionId, AzureSubscriptionId);
            variables.Set(Deployment.SpecialVariables.Action.Azure.TenantId, AzureTenantId);
            variables.Set(Deployment.SpecialVariables.Action.Azure.ClientId, AzureClientId);
            variables.Set(Deployment.SpecialVariables.Account.AccountType, accountType);
            if (withJwt) variables.Set(Deployment.SpecialVariables.Action.Azure.Jwt, AzureJwt);
            else variables.Set(Deployment.SpecialVariables.Action.Azure.Password, AzurePassword);
            variables.Set(SpecialVariables.AksClusterResourceGroup, AksClusterResourceGroup);
            variables.Set(SpecialVariables.AksClusterName, AksClusterName);
            variables.AddFlag(SpecialVariables.AksAdminLogin, true);

            var sut = CreateSut();

            var result = sut.Execute();

            result.VerifySuccess();

            // Skip the "which/where" "az/az.cmd" and "which/where" "kubelogin"
            // as they differ on windows and nix.
            invocations.Skip(2).Should().BeEquivalentTo(new[]
            {
                ("az", $"cloud set --name {AzureEnvironment}"),
                ("az", withJwt ?
                    $"login --service-principal --federated-token \"{AzureJwt}\" --username=\"{AzureClientId}\" --tenant=\"{AzureTenantId}\"" :
                    $"login --service-principal --username=\"{AzureClientId}\" --password=\"{AzurePassword}\" --tenant=\"{AzureTenantId}\""),
                ("az", $"account set --subscription {AzureSubscriptionId}"),
                ("az", $"aks get-credentials --resource-group {AksClusterResourceGroup} --name {AksClusterName} --file \"{Path.Combine(workingDirectory, "kubectl-octo.yml")}\" --overwrite-existing --admin"),
                ("kubectl", $"config set-context {AksClusterName}-admin --namespace={Namespace}"),
                ("kubelogin", $"convert-kubeconfig -l azurecli --kubeconfig \"{Path.Combine(workingDirectory, "kubectl-octo.yml")}\""),
                ("kubectl", $"get namespace {Namespace}"),
            }, opts => opts.WithStrictOrdering());
        }

        [Test]
        public void Execute_WithAzureButTrySetAzFails_Fails()
        {
            variables.Set(SpecialVariables.Namespace, Namespace);

            variables.Set(Deployment.SpecialVariables.Action.Azure.Environment, AzureEnvironment);
            variables.Set(Deployment.SpecialVariables.Action.Azure.SubscriptionId, AzureSubscriptionId);
            variables.Set(Deployment.SpecialVariables.Action.Azure.TenantId, AzureTenantId);
            variables.Set(Deployment.SpecialVariables.Action.Azure.ClientId, AzureClientId);
            variables.Set(Deployment.SpecialVariables.Action.Azure.Password, AzurePassword);
            variables.Set(Deployment.SpecialVariables.Account.AccountType, AccountTypes.AzureServicePrincipal);
            variables.Set(SpecialVariables.AksClusterResourceGroup, AksClusterResourceGroup);
            variables.Set(SpecialVariables.AksClusterName, AksClusterName);
            variables.AddFlag(SpecialVariables.AksAdminLogin, true);

            var sut = CreateSut();

            var result = sut.Execute();

            result.ExitCode.Should().NotBe(0);

            log.Received().Error("Could not find az. Make sure az is on the PATH.");
        }

        [TestCase(true, true, true, true, true, GoogleCloudAccountVariable, GoogleAccountJsonKey)]
        [TestCase(false, false, true, true, false, GoogleCloudAccountVariable, null)]
        [TestCase(false, true, true, false, true, null, GoogleAccountJsonKey)]
        public void Execute_WithGoogleCloudAccountType_ConfiguresAuthenticationCorrectly(bool useVmServiceAccount, bool withZone, bool withRegion, bool withProject, bool impersonateEmails, string accountVariable, string jsonKeyFromAccountVariable)
        {
            string accountType = null;
            if (useVmServiceAccount) variables.AddFlag(Deployment.SpecialVariables.Action.GoogleCloud.UseVmServiceAccount, true);
            else accountType = AccountTypes.GoogleCloudAccount;

            variables.Set(SpecialVariables.Namespace, Namespace);
            variables.Set(Deployment.SpecialVariables.Account.AccountType, accountType);
            variables.Set(Deployment.SpecialVariables.Action.GoogleCloudAccount.Variable, accountVariable);
            variables.Set(Deployment.SpecialVariables.Action.GoogleCloudAccount.JsonKeyFromAccount(accountVariable), jsonKeyFromAccountVariable != null ? ToBase64(jsonKeyFromAccountVariable) : null);
            variables.Set(Deployment.SpecialVariables.Action.GoogleCloudAccount.JsonKey, ToBase64(GoogleAccountJsonKey));

            variables.AddFlag(Deployment.SpecialVariables.Action.GoogleCloud.ImpersonateServiceAccount, impersonateEmails);
            variables.Set(Deployment.SpecialVariables.Action.GoogleCloud.ServiceAccountEmails, GoogleAccountServiceAccountEmails);

            if (withProject) variables.Set(Deployment.SpecialVariables.Action.GoogleCloud.Project, GoogleCloudProject);
            if (withRegion) variables.Set(Deployment.SpecialVariables.Action.GoogleCloud.Region, GoogleCloudRegion);
            if (withZone) variables.Set(Deployment.SpecialVariables.Action.GoogleCloud.Zone, GoogleCloudZone);

            variables.Set(SpecialVariables.GkeClusterName, GkeClusterName);
            variables.AddFlag(SpecialVariables.GkeUseClusterInternalIp, true);

            var sut = CreateSut();

            var result = sut.Execute();

            result.VerifySuccess();

            var expectedInvocations = new List<(string, string)>();

            if (!useVmServiceAccount)
            {
                expectedInvocations.Add(("gcloud", $"auth activate-service-account --key-file=\"{Path.Combine(workingDirectory, "gcpJsonKey.json")}\""));

                fileSystem.Received().WriteAllBytes($"{Path.Combine(workingDirectory, "gcpJsonKey.json")}", Arg.Is<byte[]>(b => Encoding.ASCII.GetString(b) == GoogleAccountJsonKey));
            }

            expectedInvocations.AddRange(new []
            {
                ("gcloud", $"container clusters get-credentials {GkeClusterName} --internal-ip {(withZone ? $"--zone={GoogleCloudZone}" : $"--region={GoogleCloudRegion}")}"),
                ("kubectl", $"config set-context --current --namespace={Namespace}"),
                ("kubectl", $"get namespace {Namespace}")
            });

            // Skipping "where/which" "gcloud.cmd/gcloud" because it it different on windows and nix
            invocations.Skip(1).Should().BeEquivalentTo(expectedInvocations, opts => opts.WithStrictOrdering());

            var expectedEnvVars = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("KUBECONFIG", $"{Path.Combine(workingDirectory, "kubectl-octo.yml")}"),
            };
            if (withZone) expectedEnvVars.Add(new KeyValuePair<string, string>("CLOUDSDK_COMPUTE_ZONE", GoogleCloudZone));
            if (withRegion) expectedEnvVars.Add(new KeyValuePair<string, string>("CLOUDSDK_COMPUTE_REGION", GoogleCloudRegion));
            if (withProject) expectedEnvVars.Add(new KeyValuePair<string, string>("CLOUDSDK_CORE_PROJECT", GoogleCloudProject));
            if (impersonateEmails) expectedEnvVars.Add(new KeyValuePair<string, string>("CLOUDSDK_AUTH_IMPERSONATE_SERVICE_ACCOUNT", GoogleAccountServiceAccountEmails));

            environmentVars.Should().BeEquivalentTo(expectedEnvVars);
        }

        [Test]
        public void Execute_WithGoogleCloudAuthButNoJsonKey_Fails()
        {
            variables.Set(SpecialVariables.Namespace, Namespace);

            variables.Set(Deployment.SpecialVariables.Action.GoogleCloud.Project, GoogleCloudProject);
            variables.Set(Deployment.SpecialVariables.Action.GoogleCloud.Zone, GoogleCloudZone);
            variables.Set(Deployment.SpecialVariables.Account.AccountType, AccountTypes.GoogleCloudAccount);
            variables.Set(SpecialVariables.GkeClusterName, GkeClusterName);

            var sut = CreateSut();

            var result = sut.Execute();

            result.ExitCode.Should().NotBe(0);

            log.Received().Error("Failed to authenticate with gcloud. Key file is empty.");
        }

        [Test]
        public void Execute_WithGoogleCloudAuthButNeitherZoneNorRegionSet_Fails()
        {
            variables.Set(SpecialVariables.Namespace, Namespace);

            variables.Set(Deployment.SpecialVariables.Action.GoogleCloudAccount.JsonKey, ToBase64(GoogleAccountJsonKey));
            variables.Set(Deployment.SpecialVariables.Action.GoogleCloud.Project, GoogleCloudProject);
            variables.Set(Deployment.SpecialVariables.Account.AccountType, AccountTypes.GoogleCloudAccount);
            variables.Set(SpecialVariables.GkeClusterName, GkeClusterName);

            var sut = CreateSut();

            var result = sut.Execute();

            result.ExitCode.Should().NotBe(0);

            log.Received().Error("Either zone or region must be defined");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Execute_WithPodServiceAccountAuth_ConfiguresAuthenticationCorrectly(bool skipTlsVerification)
        {
            fileSystem.FileExists(PodServiceAccountTokenPath).Returns(true);
            fileSystem.FileExists(CertificateAuthorityPath).Returns(true);
            fileSystem.ReadFile(PodServiceAccountTokenPath).Returns(PodServiceAccountToken);

            if (skipTlsVerification)
            {
                variables.AddFlag(SpecialVariables.SkipTlsVerification, true);
            }

            fileSystem.ReadFile(CertificateAuthorityPath).Returns(skipTlsVerification ? string.Empty : CertificateAuthority);

            variables.Set(SpecialVariables.ClusterUrl, ClusterUrl);
            variables.Set(SpecialVariables.Namespace, Namespace);

            variables.Set(SpecialVariables.PodServiceAccountTokenPath, PodServiceAccountTokenPath);
            variables.Set(SpecialVariables.CertificateAuthorityPath, CertificateAuthorityPath);

            var sut = CreateSut();

            var result = sut.Execute();

            result.VerifySuccess();

            var expectedInvocations = new List<(string, string)>
            {
                ("kubectl", $"config set-cluster octocluster --server={ClusterUrl}"),
            };

            if (skipTlsVerification)
            {
                expectedInvocations.Add(("kubectl", "config set-cluster octocluster --insecure-skip-tls-verify=true"));
            }
            else
            {
                expectedInvocations.Add(("kubectl", $"config set-cluster octocluster --certificate-authority={CertificateAuthorityPath}"));
            }

            expectedInvocations.AddRange(new []
            {
                ("kubectl", $"config set-context octocontext --user=octouser --cluster=octocluster --namespace={Namespace}"),
                ("kubectl", "config use-context octocontext"),
                ("kubectl", $"config set-credentials octouser --token={PodServiceAccountToken}"),
                ("kubectl", $"get namespace {Namespace}"),
            });

            invocations.Should().BeEquivalentTo(expectedInvocations, opts => opts.WithStrictOrdering());
        }

        [TestCase(false, true, false, true, "Kubernetes account type or certificate is missing")]
        [TestCase(true, false, false, true, "Pod service token file not found")]
        [TestCase(true, true, true, true, "Pod service token file is empty")]
        [TestCase(true, true, false, false, "Certificate authority file not found")]
        public void Execute_ForPodServiceAccountMissingServiceAccountOrCertificateAuthority(bool pathVariablesSet, bool accountTokenFound, bool accountTokenNull, bool certificateAuthorityFound, string errorMessage)
        {
            fileSystem.FileExists(PodServiceAccountTokenPath).Returns(accountTokenFound);
            fileSystem.FileExists(CertificateAuthorityPath).Returns(certificateAuthorityFound);
            fileSystem.ReadFile(PodServiceAccountTokenPath).Returns(accountTokenNull ? null : PodServiceAccountToken);
            fileSystem.ReadFile(CertificateAuthorityPath).Returns(CertificateAuthority);

            variables.Set(SpecialVariables.ClusterUrl, ClusterUrl);
            variables.Set(SpecialVariables.Namespace, Namespace);

            if (pathVariablesSet)
            {
                variables.Set(SpecialVariables.PodServiceAccountTokenPath, PodServiceAccountTokenPath);
                variables.Set(SpecialVariables.CertificateAuthorityPath, CertificateAuthorityPath);
            }

            var sut = CreateSut();

            var result = sut.Execute();

            result.ExitCode.Should().NotBe(0);

            log.Received().Error(errorMessage);
        }

        [Test]
        public void Execute_WithInvalidAccountType_Fails()
        {
            const string accountType = "NonValidAccountType";

            variables.Set(SpecialVariables.ClusterUrl, ClusterUrl);
            variables.Set(Deployment.SpecialVariables.Account.AccountType, accountType);
            var sut = CreateSut();

            var result = sut.Execute();

            result.ExitCode.Should().NotBe(0);

            log.Received().Error($"Account Type {accountType} is currently not valid for kubectl contexts");
        }

        string ToBase64(string input)
        {
            return Convert.ToBase64String(Encoding.ASCII.GetBytes(input));
        }
    }

    // This extension method only exists in .net6.0
    #if NETFX
    public static class MiscExtensions
    {
        public static IEnumerable<T> TakeLast<T>(this IEnumerable<T> source, int N)
        {
            return source.Skip(Math.Max(0, source.Count() - N));
        }
    }
    #endif
}