#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Calamari.Azure;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.Variables;
using Microsoft.Azure.Management.AppService.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Rest;

namespace Calamari.AzureAppService.Behaviors
{
    class AzureAppServiceBehaviour : IDeployBehaviour
    {
        private ILog Log { get; }

        private IPackageProvider Archive { get; set; }

        public AzureAppServiceBehaviour(ILog log)
        {
            Log = log;
            Archive = new ZipPackageProvider();
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
            var packageFileInfo = new FileInfo(variables.Get(TentacleVariables.CurrentDeployment.PackageFilePath));

            switch (packageFileInfo.Extension)
            {
                case ".zip":
                    Archive = new ZipPackageProvider();
                    break;
                case ".nupkg":
                    Archive = new NugetPackageProvider();
                    break;
                case ".war":
                    Archive = new WarPackageProvider(Log, variables, context);
                    break;
            }

            var azureClient = Microsoft.Azure.Management.Fluent.Azure.Configure()
                .Authenticate(
                    SdkContext.AzureCredentialsFactory.FromServicePrincipal(servicePrincipal.ClientId,
                        servicePrincipal.Password, servicePrincipal.TenantId,
                       !string.IsNullOrEmpty(servicePrincipal.AzureEnvironment) ? AzureEnvironment.FromName(servicePrincipal.AzureEnvironment) : AzureEnvironment.AzureGlobalCloud))
                .WithSubscription(servicePrincipal.SubscriptionNumber);

            var webApp = await azureClient.WebApps.GetByResourceGroupAsync(resourceGroupName, webAppName);

            var targetSite = AzureWebAppHelper.GetAzureTargetSite(webAppName, slotName, resourceGroupName);

            // Lets process our archive while the slot is spun up.  we will await it later before we try to upload to it.
            var slotCreateTask = new Task(() => { });
            if (targetSite.HasSlot)
                slotCreateTask = FindOrCreateSlot(webApp, targetSite);

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

            var uploadPath = string.Empty;
            if (substitutionFeatures.Any(featureName => context.Variables.IsFeatureEnabled(featureName)))
            {
                uploadPath = (await Archive.PackageArchive(context.StagingDirectory, context.CurrentDirectory))
                    .FullName;
            }
            else
            {
                uploadPath = (await Archive.ConvertToAzureSupportedFile(packageFileInfo)).FullName;
            }

            if (uploadPath == null)
                throw new Exception("Package File Path must be specified");

            // need to ensure slot is created as slot creds may be used
            if (targetSite.HasSlot)
                await slotCreateTask;
            var credential = await Auth.GetBasicAuthCreds(servicePrincipal, targetSite);
            string token = await Auth.GetAuthTokenAsync(servicePrincipal);
            
            var webAppClient = new Microsoft.Azure.Management.WebSites.WebSiteManagementClient(new Uri(servicePrincipal.ResourceManagementEndpointBaseUri), new TokenCredentials(token))
                { SubscriptionId = servicePrincipal.SubscriptionNumber};

            var httpClient = webAppClient.HttpClient;
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credential);
            
            Log.Info($"Uploading package to {targetSite.SiteAndSlot}");

            await UploadZipAsync(httpClient, uploadPath, targetSite.ScmSiteAndSlot);
            
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

        private async Task UploadZipAsync(HttpClient client, string uploadZipPath, string targetSite)
        {
            Log.Verbose($"Path to upload: {uploadZipPath}");
            Log.Verbose($"Target Site: {targetSite}");

            if (!new FileInfo(uploadZipPath).Exists)
                throw new FileNotFoundException(uploadZipPath);

            Log.Verbose($@"Publishing {uploadZipPath} to https://{targetSite}.scm.azurewebsites.net{Archive.UploadUrlPath}");

            var response = await client.PostAsync($@"https://{targetSite}.scm.azurewebsites.net{Archive.UploadUrlPath}",
                new StreamContent(new FileStream(uploadZipPath, FileMode.Open)));

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(response.ReasonPhrase);
            }

            Log.Verbose("Finished deploying");
        }
    }
}
