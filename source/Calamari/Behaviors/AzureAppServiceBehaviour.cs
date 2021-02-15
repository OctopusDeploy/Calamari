#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Calamari.Azure;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.Variables;
using Microsoft.Azure.Management.AppService.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;

namespace Calamari.AzureAppService.Behaviors
{
    class AzureAppServiceBehaviour : IDeployBehaviour
    {
        private ILog Log { get; }

        public AzureAppServiceBehaviour(ILog log)
        {
            Log = log;
        }

        public bool IsEnabled(RunningDeployment context)
        {
            return true;
        }

        public async Task Execute(RunningDeployment context)
        {
            var variables = context.Variables;
            var servicePrincipal = new ServicePrincipalAccount(variables);
            var webAppName = variables.Get(SpecialVariables.Action.Azure.WebAppName);
            if (webAppName == null)
                throw new Exception("Web App Name must be specified");
            var resourceGroupName = variables.Get(SpecialVariables.Action.Azure.ResourceGroupName);
            if (resourceGroupName == null)
                throw new Exception("resource group name must be specified");
            var slotName = variables.Get(SpecialVariables.Action.Azure.WebAppSlot);

            var azureClient = Microsoft.Azure.Management.Fluent.Azure.Configure()
                .Authenticate(
                    SdkContext.AzureCredentialsFactory.FromServicePrincipal(servicePrincipal.ClientId,
                        servicePrincipal.Password, servicePrincipal.TenantId,
                       !string.IsNullOrEmpty(servicePrincipal.AzureEnvironment) ? AzureEnvironment.FromName(servicePrincipal.AzureEnvironment) : AzureEnvironment.AzureGlobalCloud))
                .WithSubscription(servicePrincipal.SubscriptionNumber);

            var webApp = await azureClient.WebApps.GetByResourceGroupAsync(resourceGroupName, webAppName);

            var substitutionFeatures = new[]
            {
                KnownVariables.Features.ConfigurationTransforms,
                KnownVariables.Features.StructuredConfigurationVariables,
                KnownVariables.Features.SubstituteInFiles
            };

            /*
             * Calamari default behaviors
             * https://github.com/OctopusDeploy/Calamari/tree/master/source/Calamari.Common/Features/Behaviours
             */

            var uploadZipPath = string.Empty;
            if (substitutionFeatures.Any(featureName => context.Variables.IsFeatureEnabled(featureName)))
            {

                    using var archive = ZipArchive.Create();
#pragma warning disable CS8604 // Possible null reference argument.
                archive.AddAllFromDirectory(
                    $"{context.StagingDirectory}");
#pragma warning restore CS8604 // Possible null reference argument.
                archive.SaveTo($"{context.CurrentDirectory}/app.zip", CompressionType.Deflate);
                    uploadZipPath = $"{context.CurrentDirectory}/app.zip";
            }
            else
            {
                uploadZipPath = variables.Get(TentacleVariables.CurrentDeployment.PackageFilePath);
            }

            if (uploadZipPath == null)
                throw new Exception("Package File Path must be specified");

            var targetSite = AzureWebAppHelper.GetAzureTargetSite(webAppName, slotName, resourceGroupName);
            
            // Get Authentication creds/tokens
            var credential = await Auth.GetBasicAuthCreds(principalAccount, targetSite);
            string token = await Auth.GetAuthTokenAsync(principalAccount);

            var webAppClient = new WebSiteManagementClient(new Uri(principalAccount.ResourceManagementEndpointBaseUri), new TokenCredentials(token))
                { SubscriptionId = principalAccount.SubscriptionNumber};

            var httpClient = webAppClient.HttpClient;
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credential);
            
            var targetSite = AzureWebAppHelper.GetAzureTargetSite(webAppName, slotName); 
            var slot =
                targetSite.HasSlot
                    ? await FindOrCreateSlot(webApp, targetSite)
                    : null;

            Log.Info($"Uploading package to {targetSite.SiteAndSlot}");
            if (slot != null)
            {
                slot.Deploy().WithPackageUri(uploadZipPath);
            }
            else
            {
                webApp.Deploy().WithPackageUri(uploadZipPath);
            }

            Log.Info($"Soft restarting {targetSite.SiteAndSlot}");
            await webAppClient.WebApps.RestartAsync(targetSite, true);
        }

        private async Task<IDeploymentSlot> FindOrCreateSlot(IWebApp client, TargetSite site)
        {
            Log.Verbose($"Checking if slot {site.Slot} exists");

            var slot = await client.DeploymentSlots.GetByNameAsync(site.Slot);
            if (slot != null)
            {
                Log.Verbose($"Found existing slot {site.Slot}");
                return slot;
            }

            Log.Verbose($"Slot {site.Slot} not found");
            Log.Info($"Creating slot {site.Slot}");
            return await client.DeploymentSlots
                .Define(site.Slot)
                .WithConfigurationFromParent()
                .CreateAsync();
        }
    }
}
