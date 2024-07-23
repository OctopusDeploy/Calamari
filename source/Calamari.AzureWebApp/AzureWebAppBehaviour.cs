using System;
using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Calamari.Azure;
using Calamari.AzureWebApp.Integration.Websites.Publishing;
using Calamari.AzureWebApp.Util;
using Calamari.CloudAccounts;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.Variables;
using Microsoft.Web.Deployment;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.AzureWebApp
{
    class AzureWebAppBehaviour : IDeployBehaviour
    {
        readonly ILog log;
        readonly ResourceManagerPublishProfileProvider resourceManagerPublishProfileProvider;

        public AzureWebAppBehaviour(ILog log, ResourceManagerPublishProfileProvider resourceManagerPublishProfileProvider)
        {
            this.log = log;
            this.resourceManagerPublishProfileProvider = resourceManagerPublishProfileProvider;
        }

        public bool IsEnabled(RunningDeployment context)
        {
            return true;
        }

        public async Task Execute(RunningDeployment deployment)
        {
            var variables = deployment.Variables;
            var hasJwt = !variables.Get(AzureAccountVariables.Jwt).IsNullOrEmpty();
            var subscriptionId = variables.Get(SpecialVariables.Action.Azure.SubscriptionId);
            var resourceGroupName = variables.Get(SpecialVariables.Action.Azure.ResourceGroupName, string.Empty);
            var siteAndSlotName = variables.Get(SpecialVariables.Action.Azure.WebAppName);
            var slotName = variables.Get(SpecialVariables.Action.Azure.WebAppSlot);
            var targetSite = AzureWebAppHelper.GetAzureTargetSite(siteAndSlotName, slotName);
            var resourceGroupText = string.IsNullOrEmpty(resourceGroupName) ? string.Empty : $" in Resource Group '{resourceGroupName}'";
            var slotText = targetSite.HasSlot ? $", deployment slot '{targetSite.Slot}'" : string.Empty;
            var azureAccount = hasJwt ? (IAzureAccount)new AzureOidcAccount(variables) : new AzureServicePrincipalAccount(variables);

            // We will skip checking if SCM is enabled when a resource group is not provided as it's not possible, and authentication may still be valid
            if (!string.IsNullOrEmpty(resourceGroupName))
            {
                var accessToken = await azureAccount.GetAccessTokenAsync();
                var isCurrentScmBasicAuthPublishingEnabled = await AzureWebAppHelper.GetBasicPublishingCredentialsPoliciesAsync(azureAccount.ResourceManagementEndpointBaseUri,
                                                                                                                                subscriptionId,
                                                                                                                                resourceGroupName,
                                                                                                                                siteAndSlotName,
                                                                                                                                accessToken);
                if (!isCurrentScmBasicAuthPublishingEnabled)
                {
                    log.Error($"The 'SCM Basic Auth Publishing Credentials' configuration is disabled on '{targetSite.Site}'-{slotText}. To deploy Web Apps with SCM disabled, please use the 'Deploy an Azure App Service' step.");
                    throw new CommandException($"The 'SCM Basic Auth Publishing Credentials' is disabled on target '{targetSite.Site}'{slotText}");
                }
            }
            else
            {
                log.Warn($"No Resource Group Name was provided. Checking 'SCM Basic Auth Publishing Credentials' configuration will be skipped. If authentication fails, ensure 'SCM Basic Auth Publishing Credentials' is enabled on '{targetSite.Site}'-{slotText}. To deploy Web Apps with SCM disabled, please use the 'Deploy an Azure App Service' step.");
            }


            log.Info($"Deploying to Azure WebApp '{targetSite.Site}'{slotText}{resourceGroupText}, using subscription-id '{subscriptionId}'");
            var publishSettings = await GetPublishProfile(variables, azureAccount);
            RemoteCertificateValidationCallback originalServerCertificateValidationCallback = null;
            try
            {
                originalServerCertificateValidationCallback = ServicePointManager.ServerCertificateValidationCallback;
                ServicePointManager.ServerCertificateValidationCallback = WrapperForServerCertificateValidationCallback;
                await DeployToAzure(deployment, targetSite, variables, publishSettings);
            }
            finally
            {
                ServicePointManager.ServerCertificateValidationCallback = originalServerCertificateValidationCallback;
            }
        }

        async Task DeployToAzure(RunningDeployment deployment, AzureTargetSite targetSite,
            IVariables variables,
            WebDeployPublishSettings publishSettings)
        {
            var retry = AzureRetryTracker.GetDefaultRetryTracker();
            while (retry.Try())
            {
                try
                {
                    log.Verbose($"Using site '{targetSite.Site}'");
                    log.Verbose($"Using slot '{targetSite.Slot}'");
                    var changeSummary = DeploymentManager
                        .CreateObject("contentPath", deployment.CurrentDirectory)
                        .SyncTo(
                            "contentPath",
                            BuildPath(targetSite, variables),
                            DeploymentOptions(publishSettings),
                            DeploymentSyncOptions(variables)
                        );

                    log.InfoFormat(
                        "Successfully deployed to Azure. {0} objects added. {1} objects updated. {2} objects deleted.",
                        changeSummary.ObjectsAdded, changeSummary.ObjectsUpdated, changeSummary.ObjectsDeleted);
                    break;
                }
                catch (DeploymentDetailedException ex)
                {
                    if (retry.CanRetry())
                    {
                        if (retry.ShouldLogWarning())
                        {
                            Log.VerboseFormat("Retry #{0} on Azure deploy. Exception: {1}", retry.CurrentTry,
                                ex.Message);
                        }

                        await Task.Delay(retry.Sleep());
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }

        bool WrapperForServerCertificateValidationCallback(object sender, X509Certificate certificate,
            X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            switch (sslPolicyErrors)
            {
                case SslPolicyErrors.None:
                    return true;
                case SslPolicyErrors.RemoteCertificateNameMismatch:
                    log.Error(
                        $"A certificate mismatch occurred. We have had reports previously of Azure using incorrect certificates for some Web App SCM sites, which seem to related to a known issue, a possible fix is documented in {log.FormatLink("https://g.octopushq.com/CertificateMismatch")}.");
                    break;
            }

            return false;
        }

        Task<WebDeployPublishSettings> GetPublishProfile(IVariables variables, IAzureAccount account)
        {
            var siteAndSlotName = variables.Get(SpecialVariables.Action.Azure.WebAppName);
            var slotName = variables.Get(SpecialVariables.Action.Azure.WebAppSlot);
            var targetSite = AzureWebAppHelper.GetAzureTargetSite(siteAndSlotName, slotName);

            return resourceManagerPublishProfileProvider.GetPublishProperties(account,
                variables.Get(SpecialVariables.Action.Azure.ResourceGroupName, string.Empty),
                targetSite);
        }

        string BuildPath(AzureTargetSite site, IVariables variables)
        {
            var relativePath = variables.Get(SpecialVariables.Action.Azure.PhysicalPath, String.Empty).TrimStart('\\');
            return relativePath != String.Empty
                ? site.Site + "\\" + relativePath
                : site.Site;
        }

        DeploymentBaseOptions DeploymentOptions(WebDeployPublishSettings settings)
        {
            var publishProfile = settings.PublishProfile;
            var deploySite = settings.DeploymentSite;

            var options = new DeploymentBaseOptions
            {
                AuthenticationType = "Basic",
                RetryAttempts = 3,
                RetryInterval = 1000,
                TraceLevel = TraceLevel.Verbose,
                UserName = publishProfile.UserName,
                Password = publishProfile.Password,
                UserAgent = "OctopusDeploy/1.0",
                ComputerName = new Uri(publishProfile.Uri, $"/msdeploy.axd?site={deploySite}").ToString()
            };
            options.Trace += (sender, eventArgs) => LogDeploymentEvent(eventArgs);
            return options;
        }

        DeploymentSyncOptions DeploymentSyncOptions(IVariables variables)
        {
            var syncOptions = new DeploymentSyncOptions
            {
                WhatIf = false,
                UseChecksum = variables.GetFlag(SpecialVariables.Action.Azure.UseChecksum),
                DoNotDelete = !variables.GetFlag(SpecialVariables.Action.Azure.RemoveAdditionalFiles)
            };

            ApplyAppOfflineDeploymentRule(syncOptions, variables);
            ApplyPreserveAppDataDeploymentRule(syncOptions, variables);
            ApplyPreservePathsDeploymentRule(syncOptions, variables);
            return syncOptions;
        }

        void ApplyPreserveAppDataDeploymentRule(DeploymentSyncOptions syncOptions,
            IVariables variables)
        {
            // If PreserveAppData variable set, then create SkipDelete rules for App_Data directory
            // ReSharper disable once InvertIf
            if (variables.GetFlag(SpecialVariables.Action.Azure.PreserveAppData))
            {
                syncOptions.Rules.Add(new DeploymentSkipRule("SkipDeleteDataFiles", "Delete", "filePath",
                    "\\\\App_Data\\\\.*", null));
                syncOptions.Rules.Add(new DeploymentSkipRule("SkipDeleteDataDir", "Delete", "dirPath",
                    "\\\\App_Data(\\\\.*|$)", null));
            }
        }

        void ApplyPreservePathsDeploymentRule(DeploymentSyncOptions syncOptions,
                                              IVariables variables)
        {
            // If PreservePaths variable set, then create SkipDelete rules for each path regex
            var preservePaths = variables.GetStrings(SpecialVariables.Action.Azure.PreservePaths, ';');

            for (var i = 0; i < preservePaths.Count; i++)
            {
                var path = preservePaths[i];
                syncOptions.Rules.Add(new DeploymentSkipRule($"SkipDeleteFiles_{i}",
                                                             "Delete",
                                                             "filePath",
                                                             path,
                                                             null));
                syncOptions.Rules.Add(new DeploymentSkipRule($"SkipDeleteDir_{i}",
                                                             "Delete",
                                                             "dirPath",
                                                             path,
                                                             null));
            }
        }

        void ApplyAppOfflineDeploymentRule(DeploymentSyncOptions syncOptions,
                                           IVariables variables)
        {
            // ReSharper disable once InvertIf
            if (variables.GetFlag(SpecialVariables.Action.Azure.AppOffline))
            {
                var rules = Microsoft.Web.Deployment.DeploymentSyncOptions.GetAvailableRules();
                if (rules.TryGetValue("AppOffline", out var rule))
                    syncOptions.Rules.Add(rule);
                else
                    log.Verbose("Azure Deployment API does not support `AppOffline` deployment rule.");
            }
        }

        void LogDeploymentEvent(DeploymentTraceEventArgs args)
        {
            switch (args.EventLevel)
            {
                case TraceLevel.Verbose:
                    log.Verbose(args.Message);
                    break;
                case TraceLevel.Info:
                    // The deploy-log is noisy; we'll log info as verbose
                    log.Verbose(args.Message);
                    break;
                case TraceLevel.Warning:
                    log.Warn(args.Message);
                    break;
                case TraceLevel.Error:
                    log.Error(args.Message);
                    break;
            }
        }
    }
}