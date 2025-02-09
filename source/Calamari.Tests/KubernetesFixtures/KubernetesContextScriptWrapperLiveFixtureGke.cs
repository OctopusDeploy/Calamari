#if NETCORE
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Testing;
using Calamari.Testing.Helpers;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using SpecialVariables = Calamari.Kubernetes.SpecialVariables;

namespace Calamari.Tests.KubernetesFixtures
{
    [TestFixture]
    [Category(TestCategory.CompatibleOS.OnlyWindows)]
    public class KubernetesContextScriptWrapperLiveFixtureGke : KubernetesContextScriptWrapperLiveFixture
    {
        string gkeToken;
        string gkeProject;
        string gkeLocation;
        string gkeClusterCaCertificate;
        string gkeClusterEndpoint;
        string gkeClusterName;
        
        static readonly CancellationTokenSource CancellationTokenSource = new CancellationTokenSource();
        readonly CancellationToken cancellationToken = CancellationTokenSource.Token;

        protected override string KubernetesCloudProvider => "GKE";

        protected override IEnumerable<string> ToolsToAddToPath(InstallTools tools)
        {
            yield return tools.GcloudExecutable;
        }

        protected override async Task InstallOptionalTools(InstallTools tools)
        {
            await tools.InstallGCloud();
        }

        protected override void ExtractVariablesFromTerraformOutput(JObject jsonOutput)
        {
            gkeClusterEndpoint = jsonOutput["gke_cluster_endpoint"]["value"].Value<string>();
            gkeClusterCaCertificate = jsonOutput["gke_cluster_ca_certificate"]["value"].Value<string>();
            gkeToken = jsonOutput["gke_token"]["value"].Value<string>();
            gkeClusterName = jsonOutput["gke_cluster_name"]["value"].Value<string>();
            gkeProject = jsonOutput["gke_cluster_project"]["value"].Value<string>();
            gkeLocation = jsonOutput["gke_cluster_location"]["value"].Value<string>();
        }

        protected override async Task<Dictionary<string, string>> GetEnvironmentVars(CancellationToken cancellationToken)
        {
            return new Dictionary<string, string>
            {
                { "GOOGLE_CLOUD_KEYFILE_JSON", await ExternalVariables.Get(ExternalVariable.GoogleCloudJsonKeyfile, cancellationToken) },
                { "USE_GKE_GCLOUD_AUTH_PLUGIN", "True" },
                { "TF_VAR_static_resource_prefix", StaticTestResourcePrefix }
            };
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void AuthorisingWithToken(bool runAsScript)
        {
            variables.Set(SpecialVariables.ClusterUrl, $"https://{gkeClusterEndpoint}");
            variables.Set(Deployment.SpecialVariables.Account.AccountType, "Token");
            variables.Set(Deployment.SpecialVariables.Account.Token, gkeToken);
            var certificateAuthority = "myauthority";
            variables.Set("Octopus.Action.Kubernetes.CertificateAuthority", certificateAuthority);
            variables.Set($"{certificateAuthority}.CertificatePem", gkeClusterCaCertificate);
            if (runAsScript)
            {
                DeployWithKubectlTestScriptAndVerifyResult();
            }
            else
            {
                ExecuteCommandAndVerifyResult(TestableKubernetesDeploymentCommand.Name);
            }
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public async Task AuthorisingWithGoogleCloudAccount(bool runAsScript)
        {
            variables.Set(Deployment.SpecialVariables.Account.AccountType, "GoogleCloudAccount");
            variables.Set(SpecialVariables.GkeClusterName, gkeClusterName);
            var account = "gke_account";
            variables.Set("Octopus.Action.GoogleCloudAccount.Variable", account);
            var jsonKey = await ExternalVariables.Get(ExternalVariable.GoogleCloudJsonKeyfile, cancellationToken);
            variables.Set($"{account}.JsonKey", Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonKey)));
            variables.Set("Octopus.Action.GoogleCloud.Project", gkeProject);
            variables.Set("Octopus.Action.GoogleCloud.Zone", gkeLocation);
            if (runAsScript)
            {
                DeployWithKubectlTestScriptAndVerifyResult();
            }
            else
            {
                ExecuteCommandAndVerifyResult(TestableKubernetesDeploymentCommand.Name);
            }
        }

        [Test]
        public async Task UsingInternalIpForPrivateCluster()
        {
            variables.Set(Deployment.SpecialVariables.Account.AccountType, "GoogleCloudAccount");
            variables.Set(SpecialVariables.GkeClusterName, gkeClusterName);
            variables.Set(SpecialVariables.GkeUseClusterInternalIp, bool.TrueString);
            var account = "gke_account";
            variables.Set("Octopus.Action.GoogleCloudAccount.Variable", account);
            var jsonKey = await ExternalVariables.Get(ExternalVariable.GoogleCloudJsonKeyfile, cancellationToken);
            variables.Set($"{account}.JsonKey", Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonKey)));
            variables.Set("Octopus.Action.GoogleCloud.Project", gkeProject);
            variables.Set("Octopus.Action.GoogleCloud.Zone", gkeLocation);

            ExecuteCommandAndVerifyResult(TestableKubernetesDeploymentCommand.Name);
        }

        [Test]
        public void UnreachableK8Cluster_ShouldExecuteTargetScript()
        {
            const string unreachableClusterUrl = "https://example.kubernetes.com";
            const string certificateAuthority = "myauthority";

            variables.Set(SpecialVariables.ClusterUrl, unreachableClusterUrl);
            variables.Set(Deployment.SpecialVariables.Account.AccountType, "Token");
            variables.Set(Deployment.SpecialVariables.Account.Token, gkeToken);
            variables.Set("Octopus.Action.Kubernetes.CertificateAuthority", certificateAuthority);
            variables.Set($"{certificateAuthority}.CertificatePem", gkeClusterCaCertificate);

            DeployWithNonKubectlTestScriptAndVerifyResult();
        }
    }
}
#endif