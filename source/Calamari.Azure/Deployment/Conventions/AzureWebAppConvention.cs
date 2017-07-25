using System;
using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Calamari.Azure.Integration;
using Calamari.Azure.Integration.Websites.Publishing;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.Processes;
using Calamari.Integration.Retry;
using Microsoft.Web.Deployment;
using Octostache;

namespace Calamari.Azure.Deployment.Conventions
{
    public class AzureWebAppConvention : IInstallConvention
    {
        public void Install(RunningDeployment deployment)
        {
            var variables = deployment.Variables;
            var subscriptionId = variables.Get(SpecialVariables.Action.Azure.SubscriptionId);
            var resourceGroupName = variables.Get(SpecialVariables.Action.Azure.ResourceGroupName, string.Empty);
            var siteName = variables.Get(SpecialVariables.Action.Azure.WebAppName);

            Log.Info("Deploying to Azure WebApp '{0}'{1}, using subscription-id '{2}'",
                siteName,
                string.IsNullOrEmpty(resourceGroupName) ? string.Empty : $" in Resource Group {resourceGroupName}",
                subscriptionId);

            var publishProfile = GetPublishProfile(variables);

            RemoteCertificateValidationCallback originalServerCertificateValidationCallback = null;

            try
            {
                originalServerCertificateValidationCallback = ServicePointManager.ServerCertificateValidationCallback;
                ServicePointManager.ServerCertificateValidationCallback = WrapperForServerCertificateValidationCallback;

                DeployToAzure(deployment, siteName, variables, publishProfile);
            }
            finally
            {
                ServicePointManager.ServerCertificateValidationCallback = originalServerCertificateValidationCallback;
            }
        }

        private static void DeployToAzure(RunningDeployment deployment, string siteName, CalamariVariableDictionary variables,
            SitePublishProfile publishProfile)
        {
            var retry = GetRetryTracker();

            if (deployment.Variables.GetFlag("AllowUntrustedCertificate"))
            {
                Log.Info("Bypassing SSL validation check");
                System.Net.ServicePointManager.ServerCertificateValidationCallback = (senderX, certificate, chain, sslPolicyErrors) => true;
            }

            while (retry.Try())
            {
                try
                {
                    var changeSummary = DeploymentManager
                        .CreateObject("contentPath", deployment.CurrentDirectory)
                        .SyncTo(
                            "contentPath",
                            BuildPath(siteName, variables),
                            DeploymentOptions(siteName, publishProfile),
                            DeploymentSyncOptions(variables)
                        );

                    Log.Info(
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
            if (sslpolicyerrors == SslPolicyErrors.None)
            {
                return true;
            }

            if (sslpolicyerrors == SslPolicyErrors.RemoteCertificateNameMismatch)
            {
                Log.Error("A certificate mismatch occurred. We have had reports previously of Azure using incorrect certificates for some Web App SCM sites, which seem to related to a known issue, a possible fix is documented in https://g.octopushq.com/CertificateMismatch.");
            }

            return false;
        }

        private static SitePublishProfile GetPublishProfile(VariableDictionary variables)
        {
            var subscriptionId = variables.Get(SpecialVariables.Action.Azure.SubscriptionId);
            var siteName = variables.Get(SpecialVariables.Action.Azure.WebAppName);

            var accountType = variables.Get(SpecialVariables.Account.AccountType);

            switch (accountType)
            {
                case AzureAccountTypes.ServicePrincipalAccountType:
                    var resourceManagementEndpoint = variables.Get(SpecialVariables.Action.Azure.ResourceManagementEndPoint, DefaultVariables.ResourceManagementEndpoint);
                    if (resourceManagementEndpoint != DefaultVariables.ResourceManagementEndpoint)
                    {
                        Log.Info("Using override for resource management endpoint - {0}", resourceManagementEndpoint);
                    }

                    var activeDirectoryEndpoint = variables.Get(SpecialVariables.Action.Azure.ActiveDirectoryEndPoint, DefaultVariables.ActiveDirectoryEndpoint);
                    if (activeDirectoryEndpoint != DefaultVariables.ActiveDirectoryEndpoint)
                    {
                        Log.Info("Using override for Azure Active Directory endpoint - {0}", activeDirectoryEndpoint);
                    }

                    return ResourceManagerPublishProfileProvider.GetPublishProperties(subscriptionId,
                        variables.Get(SpecialVariables.Action.Azure.ResourceGroupName, string.Empty),
                        siteName,
                        variables.Get(SpecialVariables.Action.Azure.TenantId),
                        variables.Get(SpecialVariables.Action.Azure.ClientId),
                        variables.Get(SpecialVariables.Action.Azure.Password),
                        resourceManagementEndpoint,
                        activeDirectoryEndpoint);

                case AzureAccountTypes.ManagementCertificateAccountType:
                    var serviceManagementEndpoint = variables.Get(SpecialVariables.Action.Azure.ServiceManagementEndPoint, DefaultVariables.ServiceManagementEndpoint);
                    if (serviceManagementEndpoint != DefaultVariables.ServiceManagementEndpoint)
                    {
                        Log.Info("Using override for service management endpoint - {0}", serviceManagementEndpoint);
                    }
                    return ServiceManagementPublishProfileProvider.GetPublishProperties(subscriptionId,
                        Convert.FromBase64String(variables.Get(SpecialVariables.Action.Azure.CertificateBytes)),
                        siteName,
                        serviceManagementEndpoint);
                default:
                    throw new CommandException(
                        "Account type must be either Azure Management Certificate or Azure Service Principal");

            }
        }

        private static string BuildPath(string site, VariableDictionary variables)
        {
            var relativePath = (variables.Get(SpecialVariables.Action.Azure.PhysicalPath) ?? "").TrimStart('\\');

            return relativePath != ""
                ? site + "\\" + relativePath
                : site;
        }

        private static DeploymentBaseOptions DeploymentOptions(string siteName, SitePublishProfile publishProfile)
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
                ComputerName = new Uri(publishProfile.Uri, $"/msdeploy.axd?site={siteName}").ToString()
            };
            options.Trace += (sender, eventArgs) => LogDeploymentEvent(eventArgs);

            return options;
        }

        private static DeploymentSyncOptions DeploymentSyncOptions(VariableDictionary variables)
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

        private static void ApplyAppOfflineDeploymentRule(DeploymentSyncOptions syncOptions,
            VariableDictionary variables)
        {
            if (variables.GetFlag(SpecialVariables.Action.Azure.AppOffline))
            {
                var rules = Microsoft.Web.Deployment.DeploymentSyncOptions.GetAvailableRules();
                DeploymentRule rule;
                if (rules.TryGetValue("AppOffline", out rule))
                {
                    syncOptions.Rules.Add(rule);
                }
                else
                {
                    Log.Verbose("Azure Deployment API does not support `AppOffline` deployment rule.");
                }
            }
        }

        private static void LogDeploymentEvent(DeploymentTraceEventArgs args)
        {
            switch (args.EventLevel)
            {
                case TraceLevel.Verbose:
                    Log.Verbose(args.Message);
                    break;
                case TraceLevel.Info:
                    // The deploy-log is noisy; we'll log info as verbose
                    Log.Verbose(args.Message);
                    break;
                case TraceLevel.Warning:
                    Log.Warn(args.Message);
                    break;
                case TraceLevel.Error:
                    Log.Error(args.Message);
                    break;
            }
        }


        /// <summary>
        /// For azure operations, try again after 1s then 2s, 4s etc...
        /// </summary>
        static readonly LimitedExponentialRetryInterval RetryIntervalForAzureOperations = new LimitedExponentialRetryInterval(1000, 30000, 2);

        static RetryTracker GetRetryTracker()
        {
            return new RetryTracker(maxRetries: 3,
                timeLimit: TimeSpan.MaxValue,
                retryInterval: RetryIntervalForAzureOperations);
        }
    }
}