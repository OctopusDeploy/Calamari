#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Calamari.AzureAppService.Azure;
using Calamari.CloudAccounts;
using Calamari.Common.Commands;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.Variables;
using Microsoft.Azure.Management.AppService.Fluent;
using Microsoft.Rest;
using Octopus.CoreUtilities.Extensions;
using AccountVariables = Calamari.AzureAppService.Azure.AccountVariables;
using WebSiteManagementClient = Microsoft.Azure.Management.WebSites.WebSiteManagementClient;

namespace Calamari.AzureAppService.Behaviors
{
    internal class LegacyAzureAppServiceBehaviour : IDeployBehaviour
    {
        public LegacyAzureAppServiceBehaviour(ILog log)
        {
            Log = log;
            Archive = new ZipPackageProvider();
        }

        private ILog Log { get; }

        private IPackageProvider Archive { get; set; }

        public bool IsEnabled(RunningDeployment context) => !FeatureToggle.ModernAzureAppServiceSdkFeatureToggle.IsEnabled(context.Variables);

        public async Task Execute(RunningDeployment context)
        {
            Log.Verbose("Starting Azure App Service deployment.");

            var variables = context.Variables;
            
            var hasAccessToken = !variables.Get(AccountVariables.AssertionToken).IsNullOrEmpty();
            var account = hasAccessToken ? (IAzureAccount)new AzureOidcAccount(variables) : new AzureServicePrincipalAccount(variables);
            Log.Verbose($"Using Azure Tenant '{account.TenantId}'");
            Log.Verbose($"Using Azure Subscription '{account.SubscriptionNumber}'");
            Log.Verbose($"Using Azure ServicePrincipal AppId/ClientId '{account.ClientId}'");
            Log.Verbose($"Using Azure Cloud '{account.AzureEnvironment}'");

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

            var azureClient = account.CreateAzureClient();
            var targetSite = new AzureTargetSite(account.SubscriptionNumber, resourceGroupName, webAppName, slotName);

            Log.Verbose($"Checking existence of Resource Group '{resourceGroupName}'.");
            if (!(await azureClient.ResourceGroups.ContainAsync(resourceGroupName)))
            {
                Log.Error($"Resource Group '{resourceGroupName}' could not be found. Either it does not exist, or the Azure Account in use may not have permissions to access it.");
                throw new Exception("Resource Group not found.");
            }

            Log.Verbose($"Resource Group '{resourceGroupName}' found.");

            Log.Verbose($"Checking existence of App Service '{targetSite.Site}'.");
            var webApp = await azureClient.WebApps.GetByResourceGroupAsync(resourceGroupName, targetSite.Site);
            if (webApp == null)
            {
                Log.Error($"Azure App Service '{targetSite.Site}' could not be found in resource group '{resourceGroupName}'. Either it does not exist, or the Azure Account in use may not have permissions to access it.");
                throw new Exception($"App Service not found.");
            }

            Log.Verbose($"App Service '{targetSite.Site}' found, with Azure Resource Manager Id '{webApp.Id}'.");

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
            var slotCreateTask = new Task(() => { });
            if (targetSite.HasSlot)
                slotCreateTask = FindOrCreateSlot(webApp, targetSite);

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
            if (targetSite.HasSlot)
                await slotCreateTask;

            Log.Verbose($"Retrieving publishing profile for App Service to determine correct deployment endpoint.");
            var publishingProfile = await PublishingProfile.GetPublishingProfile(targetSite, account);
            Log.Verbose($"Using deployment endpoint '{publishingProfile.PublishUrl}' from publishing profile.");

            string? credential = await Auth.GetBasicAuthCreds(account, targetSite);
            string token = await Auth.GetAuthTokenAsync(account);

            var webAppClient = new WebSiteManagementClient(new Uri(account.ResourceManagementEndpointBaseUri),
                                                           new TokenCredentials(token))
                { SubscriptionId = account.SubscriptionNumber };

            var httpClient = webAppClient.HttpClient;
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credential);

            Log.Info($"Uploading package to {targetSite.SiteAndSlot}");

            await UploadZipAsync(publishingProfile, httpClient, uploadPath, targetSite.ScmSiteAndSlot);
        }

        private async Task<IDeploymentSlot> FindOrCreateSlot(IWebApp client, AzureTargetSite site)
        {
            Log.Verbose($"Checking if deployment slot '{site.Slot}' exists.");

            var slot = await client.DeploymentSlots.GetByNameAsync(site.Slot);
            if (slot != null)
            {
                Log.Verbose($"Found existing slot {site.Slot}");
                return slot;
            }

            Log.Verbose($"Slot '{site.Slot}' not found.");
            Log.Info($"Creating slot '{site.Slot}'.");
            return await client.DeploymentSlots
                               .Define(site.Slot)
                               .WithConfigurationFromParent()
                               .CreateAsync();
        }

        private async Task UploadZipAsync(PublishingProfile publishingProfile,
                                          HttpClient client,
                                          string uploadZipPath,
                                          string targetSite)
        {
            Log.Verbose($"Path to upload: {uploadZipPath}");
            Log.Verbose($"Target Site: {targetSite}");

            if (!new FileInfo(uploadZipPath).Exists)
                throw new FileNotFoundException(uploadZipPath);

            var zipUploadUrl = $"{publishingProfile.PublishUrl}{Archive.UploadUrlPath}";
            Log.Verbose($@"Publishing {uploadZipPath} to {zipUploadUrl}");

            // The HttpClient default timeout is 100 seconds: https://docs.microsoft.com/en-us/dotnet/api/system.net.http.httpclient.timeout?view=net-5.0#remarks
            // This timeouts with even relatively small packages: https://octopus.zendesk.com/agent/tickets/69928
            // We'll set this to an hour for now, but we should probably implement some more advanced retry logic, similar to https://github.com/OctopusDeploy/Sashimi.AzureWebApp/blob/bbea36152b2fb531c2893efedf0330a06ae0cef0/source/Calamari/AzureWebAppBehaviour.cs#L70
            client.Timeout = TimeSpan.FromHours(1);

            //we add some retry just in case the web app's Kudu/SCM is not running just yet
            var response = await RetryPolicies.TransientHttpErrorsPolicy.ExecuteAsync(async () =>
                                                                                      {
                                                                                          //we have to create a new request message each time
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

                                                                                          var r = await client.SendAsync(request);
                                                                                          r.EnsureSuccessStatusCode();
                                                                                          return r;
                                                                                      });

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Zip upload to {zipUploadUrl} failed with HTTP Status '{response.StatusCode} {response.ReasonPhrase}'.");

            Log.Verbose("Finished deploying");
        }
    }
}