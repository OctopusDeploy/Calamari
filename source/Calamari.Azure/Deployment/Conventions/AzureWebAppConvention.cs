using System;
using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Calamari.Azure.Commands;
using Calamari.Azure.Integration;
using Calamari.Azure.Integration.Websites.Publishing;
using Calamari.Azure.Util;
using Calamari.Shared;
using Calamari.Shared.Retry;
using Microsoft.Web.Deployment;
using Octostache;

namespace Calamari.Azure.Deployment.Conventions
{
    public class AzureWebAppConvention : IConvention
    {
        private readonly ILog log;

        public AzureWebAppConvention(ILog log)
        {
            this.log = log;
        }
        
        public void Run(IExecutionContext deployment)
        {
            var variables = deployment.Variables;
            var subscriptionId = variables.Get(SpecialVariables.Action.Azure.SubscriptionId);
            var resourceGroupName = variables.Get(SpecialVariables.Action.Azure.ResourceGroupName, string.Empty);
            var siteAndSlotName = variables.Get(SpecialVariables.Action.Azure.WebAppName);
            var slotName = variables.Get(SpecialVariables.Action.Azure.WebAppSlot);

            var targetSite = AzureWebAppHelper.GetAzureTargetSite(siteAndSlotName, slotName);
            
            var resourceGroupText = string.IsNullOrEmpty(resourceGroupName)
                ? string.Empty
                : $" in Resource Group '{resourceGroupName}'";
            var slotText = targetSite.HasSlot
                ? $", deployment slot '{targetSite.Slot}'" 
                : string.Empty;
            log.Info($"Deploying to Azure WebApp '{targetSite.Site}'{slotText}{resourceGroupText}, using subscription-id '{subscriptionId}'");

            var publishProfile = GetPublishProfile(variables);
            RemoteCertificateValidationCallback originalServerCertificateValidationCallback = null;
            try
            {
                originalServerCertificateValidationCallback = ServicePointManager.ServerCertificateValidationCallback;
                ServicePointManager.ServerCertificateValidationCallback = WrapperForServerCertificateValidationCallback;
                DeployToAzure(deployment, targetSite, publishProfile);
            }
            finally
            {
                ServicePointManager.ServerCertificateValidationCallback = originalServerCertificateValidationCallback;
            }
        }

        private void DeployToAzure(IExecutionContext deployment, AzureTargetSite targetSite, SitePublishProfile publishProfile)
        {
            var retry = GetRetryTracker();
            while (retry.Try())
            {
                try
                {
                    log.Verbose($"Using site {targetSite.Site}");
                    log.Verbose($"Using slot {targetSite.Slot}");
                    var changeSummary = DeploymentManager
                        .CreateObject("contentPath", deployment.CurrentDirectory)
                        .SyncTo(
                            "contentPath",
                            BuildPath(targetSite, deployment.Variables),
                            DeploymentOptions(targetSite, publishProfile),
                            DeploymentSyncOptions(deployment.Variables)
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
                            log.VerboseFormat("Retry #{0} on Azure deploy. Exception: {1}", retry.CurrentTry, ex.Message);
                        }
                        Thread.Sleep(retry.Sleep());
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }

        private bool WrapperForServerCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslpolicyerrors)
        {
            switch (sslpolicyerrors)
            {
                case SslPolicyErrors.None:
                    return true;
                case SslPolicyErrors.RemoteCertificateNameMismatch:
                    log.Error("A certificate mismatch occurred. We have had reports previously of Azure using incorrect certificates for some Web App SCM sites, which seem to related to a known issue, a possible fix is documented in https://g.octopushq.com/CertificateMismatch.");
                    break;
            }
            return false;
        }

        private SitePublishProfile GetPublishProfile(VariableDictionary variables)
        {
            var subscriptionId = variables.Get(SpecialVariables.Action.Azure.SubscriptionId);
            var accountType = variables.Get(SpecialVariables.Account.AccountType);
            var siteAndSlotName = variables.Get(SpecialVariables.Action.Azure.WebAppName);
            var slotName = variables.Get(SpecialVariables.Action.Azure.WebAppSlot);

            var targetSite = AzureWebAppHelper.GetAzureTargetSite(siteAndSlotName, slotName);

            switch (accountType)
            {
                case AzureAccountTypes.ServicePrincipalAccountType:
                    var resourceManagementEndpoint = variables.Get(SpecialVariables.Action.Azure.ResourceManagementEndPoint, DefaultVariables.ResourceManagementEndpoint);
                    if (resourceManagementEndpoint != DefaultVariables.ResourceManagementEndpoint)
                        log.InfoFormat("Using override for resource management endpoint - {0}", resourceManagementEndpoint);

                    var activeDirectoryEndpoint = variables.Get(SpecialVariables.Action.Azure.ActiveDirectoryEndPoint, DefaultVariables.ActiveDirectoryEndpoint);
                    if (activeDirectoryEndpoint != DefaultVariables.ActiveDirectoryEndpoint)
                        log.InfoFormat("Using override for Azure Active Directory endpoint - {0}", activeDirectoryEndpoint);
                    
                    return ResourceManagerPublishProfileProvider.GetPublishProperties(subscriptionId, variables.Get(SpecialVariables.Action.Azure.ResourceGroupName, string.Empty), targetSite, variables.Get(SpecialVariables.Action.Azure.TenantId), variables.Get(SpecialVariables.Action.Azure.ClientId), variables.Get(SpecialVariables.Action.Azure.Password), resourceManagementEndpoint, activeDirectoryEndpoint);

                case AzureAccountTypes.ManagementCertificateAccountType:
                    var serviceManagementEndpoint = variables.Get(SpecialVariables.Action.Azure.ServiceManagementEndPoint, DefaultVariables.ServiceManagementEndpoint);
                    if (serviceManagementEndpoint != DefaultVariables.ServiceManagementEndpoint)
                        log.InfoFormat("Using override for service management endpoint - {0}", serviceManagementEndpoint);

                    return ServiceManagementPublishProfileProvider.GetPublishProperties(subscriptionId,
                        Convert.FromBase64String(variables.Get(SpecialVariables.Action.Azure.CertificateBytes)),
                        targetSite,
                        serviceManagementEndpoint);
                default:
                    throw new CommandException(
                        "Account type must be either Azure Management Certificate or Azure Service Principal");
            }
        }

        private static string BuildPath(AzureTargetSite site, VariableDictionary variables)
        {
            var relativePath = (variables.Get(SpecialVariables.Action.Azure.PhysicalPath) ?? "").TrimStart('\\');
            return relativePath != ""
                ? site.Site + "\\" + relativePath
                : site.Site;
        }

        private DeploymentBaseOptions DeploymentOptions(AzureTargetSite targetSite, SitePublishProfile publishProfile)
        {
            var options = new DeploymentBaseOptions
            {
                AuthenticationType = "Basic",
                RetryAttempts = 3,
                RetryInterval = 1000,
                TraceLevel = TraceLevel.Verbose,
                UserName = publishProfile.UserName,
                Password = publishProfile.Password,
                UserAgent = "OctopusDeploy/1.0",
                ComputerName = new Uri(publishProfile.Uri, $"/msdeploy.axd?site={targetSite.Site}").ToString()
            };
            options.Trace += (sender, eventArgs) => LogDeploymentEvent(eventArgs);

            return options;
        }

        private DeploymentSyncOptions DeploymentSyncOptions(VariableDictionary variables)
        {
            var syncOptions = new DeploymentSyncOptions
            {
                WhatIf = false,
                UseChecksum = variables.GetFlag(SpecialVariables.Action.Azure.UseChecksum),
                DoNotDelete = !variables.GetFlag(SpecialVariables.Action.Azure.RemoveAdditionalFiles),
            };

            ApplyAppOfflineDeploymentRule(syncOptions, variables);
            ApplyPreserveAppDataDeploymentRule(syncOptions, variables);
            ApplyPreservePathsDeploymentRule(syncOptions, variables);
            return syncOptions;
        }

        private static void ApplyPreserveAppDataDeploymentRule(DeploymentSyncOptions syncOptions,
            VariableDictionary variables)
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

        private static void ApplyPreservePathsDeploymentRule(DeploymentSyncOptions syncOptions,
            VariableDictionary variables)
        {
            // If PreservePaths variable set, then create SkipDelete rules for each path regex
            var preservePaths = variables.GetStrings(SpecialVariables.Action.Azure.PreservePaths, ';');
            // ReSharper disable once InvertIf
            if (preservePaths != null)
            {
                for (var i = 0; i < preservePaths.Count; i++)
                {
                    var path = preservePaths[i];
                    syncOptions.Rules.Add(new DeploymentSkipRule("SkipDeleteFiles_" + i, "Delete", "filePath", path,
                        null));
                    syncOptions.Rules.Add(new DeploymentSkipRule("SkipDeleteDir_" + i, "Delete", "dirPath", path, null));
                }
            }
        }

        private void ApplyAppOfflineDeploymentRule(DeploymentSyncOptions syncOptions,
            VariableDictionary variables)
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

        private void LogDeploymentEvent(DeploymentTraceEventArgs args)
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

        /// <summary>
        /// For azure operations, try again after 1s then 2s, 4s etc...
        /// </summary>
        private static readonly LimitedExponentialRetryInterval RetryIntervalForAzureOperations = new LimitedExponentialRetryInterval(1000, 30000, 2);

        private static RetryTracker GetRetryTracker()
        {
            return new RetryTracker(maxRetries: 3,
                timeLimit: TimeSpan.MaxValue,
                retryInterval: RetryIntervalForAzureOperations);
        }
    }
}