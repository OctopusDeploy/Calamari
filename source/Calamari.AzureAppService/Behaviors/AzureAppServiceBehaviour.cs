#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.Resources;
using Calamari.AzureAppService.Azure;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.AzureAppService.Behaviors
{
    internal class AzureAppServiceBehaviour : IDeployBehaviour
    {
        public AzureAppServiceBehaviour(ILog log)
        {
            Log = log;
            Archive = new ZipPackageProvider();
        }

        private ILog Log { get; }

        private IPackageProvider Archive { get; set; }

        public bool IsEnabled(RunningDeployment context)
        {
            return true;
        }

        public async Task Execute(RunningDeployment context)
        {
            Log.Verbose("Starting Azure App Service deployment.");

            var variables = context.Variables;
            var servicePrincipal = ServicePrincipalAccount.CreateFromKnownVariables(variables);
            Log.Verbose($"Using Azure Tenant '{servicePrincipal.TenantId}'");
            Log.Verbose($"Using Azure Subscription '{servicePrincipal.SubscriptionNumber}'");
            Log.Verbose($"Using Azure ServicePrincipal AppId/ClientId '{servicePrincipal.ClientId}'");
            Log.Verbose($"Using Azure Cloud '{servicePrincipal.AzureEnvironment}'");

            string? resourceGroupName = variables.Get(SpecialVariables.Action.Azure.ResourceGroupName);
            if (resourceGroupName == null)
                throw new Exception("resource group name must be specified");
            Log.Verbose($"Using Azure Resource Group '{resourceGroupName}'.");

            string? webAppName = variables.Get(SpecialVariables.Action.Azure.WebAppName);
            if (webAppName == null)
                throw new Exception("Web App Name must be specified");
            Log.Verbose($"Using App Service Name '{webAppName}'.");

            string? slotName = variables.Get(SpecialVariables.Action.Azure.WebAppSlot);
            Log.Verbose(slotName == null
                            ? "No Deployment Slot specified"
                            : $"Using Deployment Slot '{slotName}'");

            var armClient = servicePrincipal.CreateArmClient();
            var targetSite = AzureWebAppHelper.GetAzureTargetSite(webAppName, slotName, resourceGroupName);

            var resourceGroups = armClient
                                 .GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(servicePrincipal.SubscriptionNumber))
                                 .GetResourceGroups();

            Log.Verbose($"Checking existence of Resource Group '{resourceGroupName}'.");
            if (!await resourceGroups.ExistsAsync(resourceGroupName))
            {
                Log.Error($"Resource Group '{resourceGroupName}' could not be found. Either it does not exist, or the Azure Account in use may not have permissions to access it.");
                throw new Exception("Resource Group not found.");
            }

            //get a reference to the resource group resource
            //this does not actually load the resource group, but we can use it later
            var resourceGroupResource = armClient.GetResourceGroupResource(ResourceGroupResource.CreateResourceIdentifier(servicePrincipal.SubscriptionNumber, resourceGroupName));

            Log.Verbose($"Resource Group '{resourceGroupName}' found.");

            Log.Verbose($"Checking existence of App Service '{targetSite.Site}'.");
            if (!await resourceGroupResource.GetWebSites().ExistsAsync(targetSite.Site))
            {
                Log.Error($"Azure App Service '{targetSite.Site}' could not be found in resource group '{resourceGroupName}'. Either it does not exist, or the Azure Account in use may not have permissions to access it.");
                throw new Exception($"App Service not found.");
            }

            var webSiteResource = armClient.GetWebSiteResource(WebSiteResource.CreateResourceIdentifier(servicePrincipal.SubscriptionNumber, resourceGroupName, targetSite.Site));
            Log.Verbose($"App Service '{targetSite.Site}' found, with Azure Resource Manager Id '{webSiteResource.Id.ToString()}'.");

            var packageFileInfo = new FileInfo(variables.Get(TentacleVariables.CurrentDeployment.PackageFilePath)!);

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
                default:
                    throw new Exception("Unsupported archive type");
            }

            // Let's process our archive while the slot is spun up. We will await it later before we try to upload to it.
            Task<WebSiteSlotResource>? slotCreateTask = null;
            if (targetSite.HasSlot)
                slotCreateTask = FindOrCreateSlot(armClient, webSiteResource, targetSite);

            string[]? substitutionFeatures =
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
                uploadPath = (await Archive.PackageArchive(context.StagingDirectory, context.CurrentDirectory)).FullName;
            else
                uploadPath = (await Archive.ConvertToAzureSupportedFile(packageFileInfo)).FullName;

            if (uploadPath == null)
                throw new Exception("Package File Path must be specified");

            // need to ensure slot is created as slot creds may be used
            WebSiteSlotResource? webSiteSlotResource = null;
            if (targetSite.HasSlot && slotCreateTask != null)
                webSiteSlotResource = await slotCreateTask;

            Log.Verbose($"Retrieving publishing profile for App Service to determine correct deployment endpoint.");
            var publishingProfile = targetSite.HasSlot switch
                                    {
                                        true => await PublishingProfile.GetPublishingProfile(webSiteSlotResource),
                                        false => await PublishingProfile.GetPublishingProfile(webSiteResource)
                                    };
            Log.Verbose($"Using deployment endpoint '{publishingProfile.PublishUrl}' from publishing profile.");

            Log.Info($"Uploading package to {targetSite.SiteAndSlot}");
            await UploadZipAsync(publishingProfile, uploadPath, targetSite.ScmSiteAndSlot);
        }

        private async Task<WebSiteSlotResource> FindOrCreateSlot(ArmClient armClient, WebSiteResource webSiteResource, TargetSite site)
        {
            Log.Verbose($"Checking if deployment slot '{site.Slot}' exists.");
            var slots = webSiteResource.GetWebSiteSlots();

            if (await slots.ExistsAsync(site.Slot))
            {
                Log.Verbose($"Found existing slot {site.Slot}");
                return armClient.GetWebSiteSlotResource(WebSiteSlotResource.CreateResourceIdentifier(webSiteResource.Id.SubscriptionId, site.ResourceGroupName, site.Site, site.Slot));
            }

            Log.Verbose($"Slot '{site.Slot}' not found.");
            Log.Info($"Creating slot '{site.Slot}'.");
            var operation = await slots.CreateOrUpdateAsync(WaitUntil.Completed,
                                                            site.Slot,
                                                            webSiteResource.Data);

            return operation.Value;
        }

        private async Task UploadZipAsync(PublishingProfile publishingProfile,
                                          string uploadZipPath,
                                          string targetSite)
        {
            Log.Verbose($"Path to upload: {uploadZipPath}");
            Log.Verbose($"Target Site: {targetSite}");

            if (!new FileInfo(uploadZipPath).Exists)
                throw new FileNotFoundException(uploadZipPath);

            var zipUploadUrl = $"{publishingProfile.PublishUrl}{Archive.UploadUrlPath}";
            Log.Verbose($@"Publishing {uploadZipPath} to {zipUploadUrl}");

            using var httpClient = new HttpClient(new HttpClientHandler
            {
#pragma warning disable DE0003
                Proxy = WebRequest.DefaultWebProxy
#pragma warning restore DE0003
            })
            {
                // The HttpClient default timeout is 100 seconds: https://docs.microsoft.com/en-us/dotnet/api/system.net.http.httpclient.timeout?view=net-5.0#remarks
                // This timeouts with even relatively small packages: https://octopus.zendesk.com/agent/tickets/69928
                // We'll set this to an hour for now, but we should probably implement some more advanced retry logic, similar to https://github.com/OctopusDeploy/Sashimi.AzureWebApp/blob/bbea36152b2fb531c2893efedf0330a06ae0cef0/source/Calamari/AzureWebAppBehaviour.cs#L70
                Timeout = TimeSpan.FromHours(1)
            };

            var request = new HttpRequestMessage(HttpMethod.Post, zipUploadUrl)
            {
                Headers =
                {
                    Authorization = new AuthenticationHeaderValue("Basic", publishingProfile.GetBasicAuthCredentials())
                },
                Content = new StreamContent(new FileStream(uploadZipPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    Headers = { ContentType = new MediaTypeHeaderValue("application/octet-stream") }
                }
            };

            //we add some retry just in case the web app's Kudu/SCM is not running just yet
            var response = await RetryPolicies.TransientHttpErrorsPolicy.ExecuteAsync(async () =>
                                                                                      {
                                                                                          var r = await httpClient.SendAsync(request);
                                                                                          r.EnsureSuccessStatusCode();
                                                                                          return r;
                                                                                      });

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Zip upload to {zipUploadUrl} failed with HTTP Status {(int)response.StatusCode} '{response.ReasonPhrase}'.");

            Log.Verbose("Finished deploying");
        }
    }
}