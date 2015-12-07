using System;
using System.Diagnostics;
using Calamari.Azure.Integration.Websites.Publishing;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Microsoft.Web.Deployment;
using Octostache;

namespace Calamari.Azure.Deployment.Conventions
{
    public class AzureWebAppConvention : IInstallConvention
    {
        readonly VariableDictionary variables;

        public AzureWebAppConvention(VariableDictionary variables)
        {
            this.variables = variables;
        }

        public void Install(RunningDeployment deployment)
        {
            var subscriptionId = variables.Get(SpecialVariables.Action.Azure.SubscriptionId);
            var siteName = variables.Get(SpecialVariables.Action.Azure.WebAppName);

            Log.Info("Deploying to Azure WebApp '{0}' using subscription-id '{1}'", siteName, subscriptionId);

            var publishProfile = GetPublishProfile(variables);

            var changeSummary = DeploymentManager
                .CreateObject("contentPath", deployment.CurrentDirectory)
                .SyncTo("contentPath", BuildPath(siteName, variables), DeploymentOptions(siteName, publishProfile), 
                DeploymentSyncOptions(variables)
                );

            Log.Info("Successfully deployed to Azure. {0} objects added. {1} objects updated. {2} objects deleted.",
                changeSummary.ObjectsAdded, changeSummary.ObjectsUpdated, changeSummary.ObjectsDeleted);
        }

        private static SitePublishProfile GetPublishProfile(VariableDictionary variables)
        {
            var subscriptionId = variables.Get(SpecialVariables.Action.Azure.SubscriptionId);
            var siteName = variables.Get(SpecialVariables.Action.Azure.WebAppName);

            var accountType = variables.Get(SpecialVariables.Account.AccountType);
            switch (accountType)
            {
                case AzureAccountTypes.ServicePrincipalAccountType:
                    return ResourceManagerPublishProfileProvider.GetPublishProperties(subscriptionId, siteName,
                        variables.Get(SpecialVariables.Action.Azure.TenantId),
                        variables.Get(SpecialVariables.Action.Azure.ClientId),
                        variables.Get(SpecialVariables.Action.Azure.Password)); 

                case AzureAccountTypes.ManagementCertificateAccountType:
                    return ServiceManagementPublishProfileProvider.GetPublishProperties(subscriptionId,
                        Convert.FromBase64String(variables.Get(SpecialVariables.Action.Azure.CertificateBytes)), 
                        siteName);
                default:
                    throw new CommandException("Account type must be either Azure Management Certificate or Azure Service Principal");

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
                UseChecksum = true,
                DoNotDelete = !variables.GetFlag(SpecialVariables.Action.Azure.RemoveAdditionalFiles, false)
            };

            // If PreserveAppData variable set, then create SkipDelete rules for App_Data directory 
            if (variables.GetFlag(SpecialVariables.Action.Azure.PreserveAppData, false))
            {
               syncOptions.Rules.Add(new DeploymentSkipRule("SkipDeleteDataFiles", "Delete", "filePath", "\\\\App_Data\\\\.*", null)); 
               syncOptions.Rules.Add(new DeploymentSkipRule("SkipDeleteDataDir", "Delete", "dirPath", "\\\\App_Data(\\\\.*|$)", null)); 
            }

            // If PreservePaths variable set, then create SkipDelete rules for each path regex
            var preservePaths = variables.GetStrings(SpecialVariables.Action.Azure.PreservePaths, ';');
            if (preservePaths != null)
            {
                for (var i = 0; i < preservePaths.Count; i++)
                {
                   var path = preservePaths[i];
                   syncOptions.Rules.Add(new DeploymentSkipRule("SkipDeleteFiles_" + i, "Delete", "filePath", path, null)); 
                   syncOptions.Rules.Add(new DeploymentSkipRule("SkipDeleteDir_" + i, "Delete", "dirPath", path, null)); 
                }
            }

            return syncOptions;
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

    }

}