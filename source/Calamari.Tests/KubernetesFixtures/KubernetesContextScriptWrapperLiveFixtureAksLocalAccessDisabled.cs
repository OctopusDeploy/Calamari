#if NETCORE
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Azure;
using Calamari.CloudAccounts;
using Calamari.Common.Features.Discovery;
using Calamari.Common.Plumbing.ServiceMessages;
using Calamari.Deployment;
using Calamari.Kubernetes.Commands;
using Calamari.Testing;
using Calamari.Testing.Helpers;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures
{
    [TestFixture]
    [Category(TestCategory.RunOnceOnWindowsAndLinux)]
    public class KubernetesContextScriptWrapperLiveFixtureAksLocalAccessDisabled : KubernetesContextScriptWrapperLiveFixture
    {
        string aksClusterName;
        string azurermResourceGroup;
        string azureSubscriptionId;

        protected override string KubernetesCloudProvider => "AKS-local-access-disabled";

        protected override IEnumerable<string> ToolsToAddToPath(InstallTools tools)
        {
            yield return tools.KubeloginExecutable;
        }

        protected override async Task InstallOptionalTools(InstallTools tools)
        {
            await tools.InstallKubelogin();
        }

        protected override void ExtractVariablesFromTerraformOutput(JObject jsonOutput)
        {
            aksClusterName = jsonOutput.Get<string>("aks_cluster_name", "value");
            azurermResourceGroup = jsonOutput.Get<string>("aks_rg_name", "value");
        }

        protected override async Task<Dictionary<string, string>> GetEnvironmentVars(CancellationToken cancellationToken)
        {
            azureSubscriptionId = await ExternalVariables.Get(ExternalVariable.AzureSubscriptionId, cancellationToken);
            return new Dictionary<string, string>()
            {
                { "ARM_SUBSCRIPTION_ID", azureSubscriptionId },
                { "ARM_CLIENT_ID", await ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId, cancellationToken) },
                { "ARM_CLIENT_SECRET", await ExternalVariables.Get(ExternalVariable.AzureSubscriptionPassword, cancellationToken) },
                { "ARM_TENANT_ID", await ExternalVariables.Get(ExternalVariable.AzureSubscriptionTenantId, cancellationToken) },
                { "TF_VAR_aks_client_id", await ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId, cancellationToken) },
                { "TF_VAR_aks_client_secret", await ExternalVariables.Get(ExternalVariable.AzureSubscriptionPassword, cancellationToken) },
                { "TF_VAR_test_namespace", TestNamespace },
            };
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public async Task AuthorisingWithAzureServicePrincipal(bool runAsScript)
        {
            variables.Set(SpecialVariables.Account.AccountType, "AzureServicePrincipal");
            variables.Set("Octopus.Action.Kubernetes.AksClusterResourceGroup", azurermResourceGroup);
            variables.Set(Kubernetes.SpecialVariables.AksClusterName, aksClusterName);
            variables.Set("Octopus.Action.Kubernetes.AksAdminLogin", Boolean.FalseString);
            variables.Set("Octopus.Action.Azure.SubscriptionId", await ExternalVariables.Get(ExternalVariable.AzureSubscriptionId, CancellationToken.None));
            variables.Set("Octopus.Action.Azure.TenantId", await ExternalVariables.Get(ExternalVariable.AzureSubscriptionTenantId, CancellationToken.None));
            variables.Set("Octopus.Action.Azure.Password", await ExternalVariables.Get(ExternalVariable.AzureSubscriptionPassword, CancellationToken.None));
            variables.Set("Octopus.Action.Azure.ClientId", await ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId, CancellationToken.None));
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
        [TestCase(false)]
        [TestCase(true)]
        public async Task DiscoverKubernetesClusterWithAzureServicePrincipalAccount(bool setHealthCheckContainer)
        {
            var scope = new TargetDiscoveryScope("TestSpace",
                "Staging",
                "testProject",
                null,
                new[] { "discovery-role" },
                "WorkerPool-1",
                setHealthCheckContainer ? new FeedImage("MyImage:with-tag", "Feeds-123") : null);

            var account = new AzureServicePrincipalAccount(
                await ExternalVariables.Get(ExternalVariable.AzureSubscriptionId, CancellationToken.None),
                await ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId, CancellationToken.None),
                await ExternalVariables.Get(ExternalVariable.AzureSubscriptionTenantId, CancellationToken.None),
                await ExternalVariables.Get(ExternalVariable.AzureSubscriptionPassword, CancellationToken.None),
                null,
                null,
                null);

            var authenticationDetails =
                new AccountAuthenticationDetails<AzureServicePrincipalAccount>(
                                                                               "Azure",
                                                                               "Accounts-1",
                                                                               "ServicePrincipal",
                                                                               account);

            var targetDiscoveryContext =
                new TargetDiscoveryContext<AccountAuthenticationDetails<AzureServicePrincipalAccount>>(scope,
                                                                                                       authenticationDetails);

            ExecuteDiscoveryCommandAndVerifyResult(targetDiscoveryContext);

            var targetName = $"aks/{azureSubscriptionId}/{azurermResourceGroup}/{aksClusterName}";
            var serviceMessageProperties = new Dictionary<string, string>
            {
                { "name", targetName },
                { "clusterName", aksClusterName },
                { "clusterResourceGroup", azurermResourceGroup },
                { "skipTlsVerification", bool.TrueString },
                { "octopusDefaultWorkerPoolIdOrName", scope.WorkerPoolId },
                { "octopusAccountIdOrName", "Accounts-1" },
                { "octopusRoles", "discovery-role" },
                { "updateIfExisting", bool.TrueString },
                { "isDynamic", bool.TrueString },
                { "awsUseWorkerCredentials", bool.FalseString },
                { "awsAssumeRole", bool.FalseString }
            };

            if (scope.HealthCheckContainer != null)
            {
                serviceMessageProperties.Add("healthCheckContainerImageFeedIdOrName", scope.HealthCheckContainer.FeedIdOrName);
                serviceMessageProperties.Add("healthCheckContainerImage", scope.HealthCheckContainer.ImageNameAndTag);
            }

            var expectedServiceMessage = new ServiceMessage(
                KubernetesDiscoveryCommand.CreateKubernetesTargetServiceMessageName,
                serviceMessageProperties);

            Log.ServiceMessages.Should()
                .ContainSingle(s => s.Properties.ContainsKey("name") && s.Properties["name"] == targetName)
                .Which.Should()
                .BeEquivalentTo(expectedServiceMessage);
        }
    }
}
#endif