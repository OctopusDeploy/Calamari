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
using Calamari.Azure;
using Calamari.AzureAppService.Azure;
using Calamari.CloudAccounts;
using Calamari.Common.Commands;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.Variables;
using Octopus.CoreUtilities.Extensions;
using Polly;
using Polly.Timeout;
using AccountVariables = Calamari.AzureAppService.Azure.AccountVariables;

namespace Calamari.AzureAppService.Behaviors
{
    internal class AzureAppServiceZipDeployBehaviour : IDeployBehaviour
    {
        public AzureAppServiceZipDeployBehaviour(ILog log)
        {
            Log = log;
        }

        private ILog Log { get; }

        public bool IsEnabled(RunningDeployment context) => true;

        public async Task Execute(RunningDeployment context)
        {
            Log.Verbose("Starting Azure App Service deployment.");

            var variables = context.Variables;
            var hasJwt = !variables.Get(AccountVariables.Jwt).IsNullOrEmpty();
            var account = hasJwt ? (IAzureAccount)new AzureOidcAccount(variables) : new AzureServicePrincipalAccount(variables);
            Log.Verbose($"Using Azure Tenant '{account.TenantId}'");
            Log.Verbose($"Using Azure Subscription '{account.SubscriptionNumber}'");
            Log.Verbose($"Using Azure ServicePrincipal AppId/ClientId '{account.ClientId}'");
            Log.Verbose($"Using Azure Cloud '{account.AzureEnvironment}'");

            var pollingTimeoutVariableValue = variables.Get(SpecialVariables.Action.Azure.AsyncZipDeploymentTimeout, "5");
            int.TryParse(pollingTimeoutVariableValue, out var pollingTimeoutValue);
            var pollingTimeout = TimeSpan.FromMinutes(pollingTimeoutValue);
            var asyncZipDeployTimeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(pollingTimeout, TimeoutStrategy.Optimistic);

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

            var armClient = account.CreateArmClient();
            var targetSite = new AzureTargetSite(account.SubscriptionNumber, resourceGroupName, webAppName, slotName);

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

            IPackageProvider packageProvider = packageFileInfo.Extension switch
                                               {
                                                   ".zip" => new ZipPackageProvider(),
                                                   ".nupkg" => new NugetPackageProvider(),
                                                   ".war" => new WarPackageProvider(Log, variables, context),
                                                   _ => throw new Exception("Unsupported archive type")
                                               };

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
             * https://github.com/OctopusDeploy/Calamari/tree/main/source/Calamari.Common/Features/Behaviours
             */
            var uploadPath = string.Empty;
            bool uploadFileNeedsCleaning = false;
            try
            {
                FileInfo? uploadFile;
                if (substitutionFeatures.Any(featureName => context.Variables.IsFeatureEnabled(featureName)))
                {
                    uploadFile = (await packageProvider.PackageArchive(context.StagingDirectory, context.CurrentDirectory));
                }
                else
                {
                    uploadFile = await packageProvider.ConvertToAzureSupportedFile(packageFileInfo);
                }
                uploadPath = uploadFile.FullName;
                uploadFileNeedsCleaning = packageFileInfo.Extension != uploadFile.Extension;

                if (uploadPath == null)
                {
                    throw new Exception("Package File Path must be specified");
                }

                // need to ensure slot is created as slot creds may be used
                if (targetSite.HasSlot && slotCreateTask != null)
                {
                    await slotCreateTask;
                }

                Log.Verbose($"Retrieving publishing profile for App Service to determine correct deployment endpoint.");
                using var publishingProfileXmlStream = await armClient.GetPublishingProfileXmlWithSecrets(targetSite);
                var publishingProfile = await PublishingProfile.ParseXml(publishingProfileXmlStream);

                Log.Verbose($"Using deployment endpoint '{publishingProfile.PublishUrl}' from publishing profile.");

                Log.Info($"Uploading package to {targetSite.SiteAndSlot}");

                //Need to check if site turn off 
                var scmPublishEnable = await armClient.IsScmPublishEnable(targetSite);
                
                if (packageProvider.SupportsAsynchronousDeployment && FeatureToggle.AsynchronousAzureZipDeployFeatureToggle.IsEnabled(context.Variables))
                {
                    await UploadZipAndPollAsync(account, publishingProfile, scmPublishEnable, uploadPath, targetSite.ScmSiteAndSlot, packageProvider, pollingTimeout, asyncZipDeployTimeoutPolicy);
                }
                else
                {
                    await UploadZipAsync(account, publishingProfile, scmPublishEnable, uploadPath, targetSite.ScmSiteAndSlot, packageProvider);
                }
            }
            finally
            {
                if (uploadFileNeedsCleaning)
                {
                    CleanupUploadFile(uploadPath);
                }
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
            var webSiteResourceData = (await webSiteResource.GetAsync()).Value.Data;
            var operation = await slots.CreateOrUpdateAsync(WaitUntil.Completed,
                                                            site.Slot,
                                                            webSiteResourceData);

            return operation.Value;
        }

        private async Task UploadZipAsync(IAzureAccount azureAccount,
                                          PublishingProfile publishingProfile,
                                          bool scmPublishEnable,
                                          string uploadZipPath,
                                          string targetSite,
                                          IPackageProvider packageProvider)
        {
            Log.Verbose($"Path to upload: {uploadZipPath}");
            Log.Verbose($"Target Site: {targetSite}");

            if (!new FileInfo(uploadZipPath).Exists)
                throw new FileNotFoundException(uploadZipPath);

            var zipUploadUrl = $"{publishingProfile.PublishUrl}{packageProvider.UploadUrlPath}";
            Log.Verbose($@"Publishing {uploadZipPath} to {zipUploadUrl}");

            var authenticationHeader = await GetAuthenticationHeaderValue(azureAccount, publishingProfile, scmPublishEnable);

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
#if NETFRAMEWORK
                                                                                          using var fileStream = new FileStream(uploadZipPath, FileMode.Open, FileAccess.Read, FileShare.Read);
#else
                                                                                          await using var fileStream = new FileStream(uploadZipPath, FileMode.Open, FileAccess.Read, FileShare.Read);
#endif
                                                                                          using var streamContent = new StreamContent(fileStream);
                                                                                          streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                                                                                          
                                                                                          //we have to create a new request message each time
                                                                                          var request = new HttpRequestMessage(HttpMethod.Post, zipUploadUrl)
                                                                                          {
                                                                                              Headers =
                                                                                              {
                                                                                                  Authorization = authenticationHeader
                                                                                              },
                                                                                              Content = streamContent
                                                                                          };

                                                                                          var r = await httpClient.SendAsync(request);
                                                                                          r.EnsureSuccessStatusCode();
                                                                                          return r;
                                                                                      });

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Zip upload to {zipUploadUrl} failed with HTTP Status {(int)response.StatusCode} '{response.ReasonPhrase}'.");

            Log.Verbose("Finished deploying");
        }

        static async Task<AuthenticationHeaderValue> GetAuthenticationHeaderValue(IAzureAccount azureAccount, PublishingProfile publishingProfile, bool scmPublishEnable)
        {
            AuthenticationHeaderValue authenticationHeader;
            if (scmPublishEnable)
            {
                authenticationHeader = new AuthenticationHeaderValue("Basic", publishingProfile.GetBasicAuthCredentials());
            }
            else
            {
                var accessToken = await azureAccount.GetAccessTokenAsync();
                authenticationHeader = new AuthenticationHeaderValue("Bearer", accessToken.Token);
            }

            return authenticationHeader;
        }

        private async Task UploadZipAndPollAsync(IAzureAccount azureAccount,
                                                 PublishingProfile publishingProfile,
                                                 bool scmPublishEnable,
                                                 string uploadZipPath,
                                                 string targetSite,
                                                 IPackageProvider packageProvider,
                                                 TimeSpan pollingTimeout,
                                                 AsyncTimeoutPolicy<HttpResponseMessage> asyncZipDeployTimeoutPolicy)
        {
            Log.Verbose($"Path to upload: {uploadZipPath}");
            Log.Verbose($"Target Site: {targetSite}");

            if (!new FileInfo(uploadZipPath).Exists)
                throw new FileNotFoundException(uploadZipPath);

            var zipUploadUrl = $"{publishingProfile.PublishUrl}{packageProvider.UploadUrlPath}?isAsync=true";
            Log.Verbose($"Publishing {uploadZipPath} to {zipUploadUrl} and checking for deployment");

            using var httpClient = new HttpClient(new HttpClientHandler
            {
#pragma warning disable DE0003
                Proxy = WebRequest.DefaultWebProxy
#pragma warning restore DE0003
            });

            var authenticationHeader = await GetAuthenticationHeaderValue(azureAccount, publishingProfile, scmPublishEnable);

            //we add some retry just in case the web app's Kudu/SCM is not running just yet
            var uploadResponse = await RetryPolicies.TransientHttpErrorsPolicy.ExecuteAsync(async () =>
                                                                                            {
                                                                                                //we have to create a new request message each time
#if NETFRAMEWORK
                                                                                                using var fileStream = new FileStream(uploadZipPath, FileMode.Open, FileAccess.Read, FileShare.Read);
#else
                                                                                                await using var fileStream = new FileStream(uploadZipPath, FileMode.Open, FileAccess.Read, FileShare.Read);
#endif
                                                                                                using var streamContent = new StreamContent(fileStream);
                                                                                                streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                                                                                                var uploadRequest = new HttpRequestMessage(HttpMethod.Post, zipUploadUrl)
                                                                                                {
                                                                                                    Headers =
                                                                                                    {
                                                                                                        Authorization = authenticationHeader
                                                                                                    },
                                                                                                    Content = streamContent
                                                                                                };

                                                                                                var r = await httpClient.SendAsync(uploadRequest);
                                                                                                r.EnsureSuccessStatusCode();
                                                                                                return r;
                                                                                            });

            if (!uploadResponse.IsSuccessStatusCode)
                throw new Exception($"Zip upload to {zipUploadUrl} failed with HTTP Status {(int)uploadResponse.StatusCode} '{uploadResponse.ReasonPhrase}'.");

            Log.Verbose("Zip upload succeeded. Monitoring for deployment completion");
            var location = uploadResponse.Headers.Location;

            //wrap the entire thing in a Polly Timeout policy which uses the cancellation token to raise the timout
            var result = await asyncZipDeployTimeoutPolicy.ExecuteAndCaptureAsync(async timeoutCancellationToken =>
                                                                                  {
                                                                                      //the outer policy should only retry when the response is a 202
                                                                                      return await RetryPolicies.AsynchronousZipDeploymentOperationPolicy
                                                                                                                .ExecuteAsync(async (_, ct1) =>
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
                                                                                                                              new Dictionary<string, object>
                                                                                                                              {
                                                                                                                                  [nameof(RetryPolicies.ContextKeys.Log)] = Log
                                                                                                                              },
                                                                                                                              timeoutCancellationToken);
                                                                                  },
                                                                                  CancellationToken.None);

            if (result.Outcome == OutcomeType.Failure)
            {
                throw result.FinalException switch
                      {
                          OperationCanceledException oce => new Exception($"Zip deployment failed to complete after {pollingTimeout}.", oce),
                          _ => new Exception($"Zip deployment failed to complete.", result.FinalException)
                      };
            }

            if (!result.Result.IsSuccessStatusCode)
                throw new Exception($"Zip deployment check failed with HTTP Status {(int)result.FinalHandledResult.StatusCode} '{result.FinalHandledResult.ReasonPhrase}'.");

            Log.Verbose("Finished zip deployment");
        }

        static void CleanupUploadFile(string? uploadPath)
        {
            Policy.Handle<IOException>()
                  .WaitAndRetry(
                                5,
                                i => TimeSpan.FromMilliseconds(200))
                  .Execute(() =>
                           {
                               if (File.Exists(uploadPath))
                               {
                                   File.Delete(uploadPath!);
                               }
                           });
        }
    }
}