#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Calamari.Azure;
using Calamari.AzureAppService.Json;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.Variables;
using Microsoft.Azure.Management.AppService.Fluent;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.Management.WebSites;
using Microsoft.Azure.Management.WebSites.Models;
using Microsoft.Rest;
using Newtonsoft.Json;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using WebSiteManagementClient = Microsoft.Azure.Management.WebSites.WebSiteManagementClient;

namespace Calamari.AzureAppService
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
                        AzureEnvironment.FromName(servicePrincipal.AzureEnvironment)))
                .WithSubscription(servicePrincipal.SubscriptionNumber);

            var webApp = await azureClient.WebApps.GetByResourceGroupAsync(resourceGroupName, webAppName);

            var substituionFeatures = new[]
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
            if (substituionFeatures.Any(featureName => context.Variables.IsFeatureEnabled(featureName)))
            {

                    using var archive = ZipArchive.Create();
#pragma warning disable CS8604 // Possible null reference argument.
                archive.AddAllFromDirectory(context.StagingDirectory);
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

            var targetSite = AzureWebAppHelper.GetAzureTargetSite(webAppName, slotName);

            string authToken = await Auth.GetAuthTokenAsync(servicePrincipal);
            var webAppClient = new WebSiteManagementClient(new Uri(servicePrincipal.ResourceManagementEndpointBaseUri), new TokenCredentials(authToken))
                { SubscriptionId = servicePrincipal.SubscriptionNumber};

            await EnsureSlotExists(webApp, targetSite);

            var publishingCredentials = await Auth.GetBasicAuthCreds(servicePrincipal, targetSite, resourceGroupName);
            var httpClient = webAppClient.HttpClient;
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", publishingCredentials);

            Log.Info($"Uploading package to {targetSite.SiteAndSlot}");
            await UploadZipAsync(httpClient, uploadZipPath, targetSite.ScmSiteAndSlot);

            Log.Info($"Soft restarting {targetSite.SiteAndSlot}");
            if (targetSite.HasSlot)
                await webAppClient.WebApps.RestartSlotWithHttpMessagesAsync(resourceGroupName, webAppName,
                    targetSite.Slot, true);
            else
                await webAppClient.WebApps.RestartAsync(resourceGroupName, webAppName, true);
        }

        private async Task UploadZipAsync(HttpClient client, string uploadZipPath, string targetSite)
        {
            Log.Verbose($"Path to upload: {uploadZipPath}");
            Log.Verbose($"Target Site: {targetSite}");

            if (!new FileInfo(uploadZipPath).Exists)
                throw new FileNotFoundException(uploadZipPath);

            Log.Verbose($@"Publishing {uploadZipPath} to https://{targetSite}.scm.azurewebsites.net/api/zipdeploy");

            var response = await client.PostAsync($@"https://{targetSite}.scm.azurewebsites.net/api/zipdeploy",
                new StreamContent(new FileStream(uploadZipPath, FileMode.Open)));

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(response.ReasonPhrase);
            }

            Log.Verbose("Finished deploying");
        }

        private async Task EnsureSlotExists(IWebApp client, TargetSite site)
        {
            if (string.IsNullOrEmpty(site.Slot))
                return;

            Log.Verbose($"Checking if slot {site.Slot} exists");

            var slot = client.DeploymentSlots.GetByNameAsync(site.Slot);
            if (slot != null)
            {
                Log.Verbose($"Found existing slot {site.Slot}");
                return;
            }

            // try
            // {
            //     var searchResult = await webAppClient.WebApps.GetSlotAsync(resourceGroup, site.Site, site.Slot);
            //
            //     if (searchResult != null)
            //     {
            //         Log.Verbose($"Found existing slot {site.Slot}");
            //         return;
            //     }
            // }
            // catch (DefaultErrorResponseException ex)
            // {
            //     // A 404 is returned if the slot doesn't exist
            //     if (ex.Response.StatusCode != HttpStatusCode.NotFound)
            //         throw;
            // }

            Log.Verbose($"Slot {site.Slot} not found");
            Log.Info($"Creating slot {site.Slot}");
            await client.DeploymentSlots
                .Define(site.Slot)
                .WithConfigurationFromParent()
                .CreateAsync();
        }
    }
}
