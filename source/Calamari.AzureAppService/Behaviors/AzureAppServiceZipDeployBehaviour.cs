#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.Resources;
using Calamari.AzureAppService.Azure;
using Calamari.Common.Commands;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.Variables;
using Polly;

namespace Calamari.AzureAppService.Behaviors
{
    internal class AzureAppServiceZipDeployBehaviour : IDeployBehaviour
    {
        public AzureAppServiceZipDeployBehaviour(ILog log)
        {
            Log = log;
            Archive = new ZipPackageProvider();
        }

        private ILog Log { get; }

        private IPackageProvider Archive { get; set; }

        public bool IsEnabled(RunningDeployment context) => FeatureToggle.ModernAzureAppServiceSdkFeatureToggle.IsEnabled(context.Variables);

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
            var targetSite = new AzureTargetSite(servicePrincipal.SubscriptionNumber, resourceGroupName, webAppName, slotName);

            var resourceGroups = armClient
                                 .GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(targetSite.SubscriptionId))
                                 .GetResourceGroups();

            Log.Verbose($"Checking existence of Resource Group '{resourceGroupName}'.");
            if (!await resourceGroups.ExistsAsync(resourceGroupName))
            {
                Log.Error($"Resource Group '{resourceGroupName}' could not be found. Either it does not exist, or the Azure Account in use may not have permissions to access it.");
                throw new Exception("Resource Group not found.");
            }

            //get a reference to the resource group resource
            //this does not actually load the resource group, but we can use it later
            var resourceGroupResource = armClient.GetResourceGroupResource(ResourceGroupResource.CreateResourceIdentifier(targetSite.SubscriptionId, resourceGroupName));

            Log.Verbose($"Resource Group '{resourceGroupName}' found.");

            Log.Verbose($"Checking existence of App Service '{targetSite.Site}'.");
            if (!await resourceGroupResource.GetWebSites().ExistsAsync(targetSite.Site))
            {
                Log.Error($"Azure App Service '{targetSite.Site}' could not be found in resource group '{resourceGroupName}'. Either it does not exist, or the Azure Account in use may not have permissions to access it.");
                throw new Exception($"App Service not found.");
            }

            var webSiteResource = armClient.GetWebSiteResource(targetSite.CreateWebSiteResourceIdentifier());
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
            if (targetSite.HasSlot && slotCreateTask != null)
                await slotCreateTask;

            Log.Verbose($"Retrieving publishing profile for App Service to determine correct deployment endpoint.");
            using var publishingProfileXmlStream = await armClient.GetPublishingProfileXmlWithSecrets(targetSite);
            var publishingProfile = await PublishingProfile.ParseXml(publishingProfileXmlStream);

            Log.Verbose($"Using deployment endpoint '{publishingProfile.PublishUrl}' from publishing profile.");

            Log.Info($"Uploading package to {targetSite.SiteAndSlot}");

            if (FeatureToggle.AsynchronousAzureZipDeployFeatureToggle.IsEnabled(context.Variables))
            {
                await UploadZipAndPollAsync(publishingProfile, uploadPath, targetSite.ScmSiteAndSlot);
            }
            else
            {
                await UploadZipAsync(publishingProfile, uploadPath, targetSite.ScmSiteAndSlot);
            }
        }

        private async Task<WebSiteSlotResource> FindOrCreateSlot(ArmClient armClient, WebSiteResource webSiteResource, AzureTargetSite site)
        {
            Log.Verbose($"Checking if deployment slot '{site.Slot}' exists.");
            var slots = webSiteResource.GetWebSiteSlots();

            if (await slots.ExistsAsync(site.Slot))
            {
                Log.Verbose($"Found existing slot {site.Slot}");
                return armClient.GetWebSiteSlotResource(site.CreateResourceIdentifier());
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

                                                                                          var r = await httpClient.SendAsync(request);
                                                                                          r.EnsureSuccessStatusCode();
                                                                                          return r;
                                                                                      });

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Zip upload to {zipUploadUrl} failed with HTTP Status {(int)response.StatusCode} '{response.ReasonPhrase}'.");

            Log.Verbose("Finished deploying");
        }

        private async Task UploadZipAndPollAsync(PublishingProfile publishingProfile,
                                                 string uploadZipPath,
                                                 string targetSite)
        {
            Log.Verbose($"Path to upload: {uploadZipPath}");
            Log.Verbose($"Target Site: {targetSite}");

            if (!new FileInfo(uploadZipPath).Exists)
                throw new FileNotFoundException(uploadZipPath);

            var zipUploadUrl = $"{publishingProfile.PublishUrl}{Archive.UploadUrlPath}?isAsync=true";
            Log.Verbose($"Publishing {uploadZipPath} to {zipUploadUrl} and checking for deployment");

            using var httpClient = new HttpClient(new HttpClientHandler
            {
#pragma warning disable DE0003
                Proxy = WebRequest.DefaultWebProxy
#pragma warning restore DE0003
            });

            var authenticationHeader = new AuthenticationHeaderValue("Basic", publishingProfile.GetBasicAuthCredentials());

            //we add some retry just in case the web app's Kudu/SCM is not running just yet
            var uploadResponse = await RetryPolicies.TransientHttpErrorsPolicy.ExecuteAsync(async () =>
                                                                                            {
                                                                                                //we have to create a new request message each time
                                                                                                var uploadRequest = new HttpRequestMessage(HttpMethod.Post, zipUploadUrl)
                                                                                                {
                                                                                                    Headers =
                                                                                                    {
                                                                                                        Authorization = authenticationHeader
                                                                                                    },
                                                                                                    Content = new StreamContent(new FileStream(uploadZipPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                                                                                                    {
                                                                                                        Headers = { ContentType = new MediaTypeHeaderValue("application/octet-stream") }
                                                                                                    }
                                                                                                };

                                                                                                var r = await httpClient.SendAsync(uploadRequest);
                                                                                                r.EnsureSuccessStatusCode();
                                                                                                return r;
                                                                                            });

            if (!uploadResponse.IsSuccessStatusCode)
                throw new Exception($"Zip upload to {zipUploadUrl} failed with HTTP Status {(int)uploadResponse.StatusCode} '{uploadResponse.ReasonPhrase}'.");

            Log.Verbose("Zip upload succeeded. Checking for deployment completion");
            var location = uploadResponse.Headers.Location;

            var pollingTimeout = TimeSpan.FromMinutes(3);

            //add a fixed 3 minute timeout to the polly operation
            var timeoutCancellationToken = new CancellationTokenSource(pollingTimeout);

            //the outer policy should only retry when the response is a 202
            var result = await RetryPolicies.AsynchronousZipDeploymentOperationPolicy
                                            .ExecuteAndCaptureAsync(async ct1 =>
                                                                        //we nest this policy so any transient errors are handled and retried. If it just keeps falling over, then we want it to bail out of the outer operation
                                                                        await RetryPolicies.TransientHttpErrorsPolicy
                                                                                           .ExecuteAsync(async ct2 =>
                                                                                                         {
                                                                                                             //we have to create a new request message each time
                                                                                                             var checkRequest = new HttpRequestMessage(HttpMethod.Get, location)
                                                                                                             {
                                                                                                                 Headers =
                                                                                                                 {
                                                                                                                     Authorization = authenticationHeader
                                                                                                                 }
                                                                                                             };

                                                                                                             var r = await httpClient.SendAsync(checkRequest, ct2);
                                                                                                             r.EnsureSuccessStatusCode();
                                                                                                             return r;
                                                                                                         },
                                                                                                         ct1),
                                                                    //pass the logger so we can log the retries
                                                                    new Context(Guid.NewGuid().ToString(), new Dictionary<string, object>
                                                                    {
                                                                        [nameof(RetryPolicies.ContextKeys.Log)] = Log
                                                                    }),
                                                                    timeoutCancellationToken.Token);

            if (result.Outcome == OutcomeType.Failure)
            {
                switch (result.FinalException)
                {
                    case OperationCanceledException oce:
                        throw new Exception($"Zip deployment failed to complete after {pollingTimeout}.", oce);
                    default:
                        throw new Exception($"Zip deployment failed to complete.", result.FinalException);
                }

            }

            if (!result.Result.IsSuccessStatusCode)
                throw new Exception($"Zip deployment check failed with HTTP Status {(int)result.FinalHandledResult.StatusCode} '{result.FinalHandledResult.ReasonPhrase}'.");

            Log.Verbose("Finished zip deployment");
        }
    }
}