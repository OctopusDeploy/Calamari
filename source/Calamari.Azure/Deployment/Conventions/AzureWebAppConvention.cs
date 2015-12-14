using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Microsoft.Web.Deployment;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Management.WebSites.Models;
using Octostache;
using PublishProfile = Microsoft.WindowsAzure.Management.WebSites.Models.WebSiteGetPublishProfileResponse.PublishProfile;

namespace Calamari.Azure.Deployment.Conventions
{
    public class AzureWebAppConvention : IInstallConvention
    {
        public void Install(RunningDeployment deployment)
        {
            var variables = deployment.Variables;
            var subscriptionId = variables.Get(SpecialVariables.Action.Azure.SubscriptionId);
            var certificate = Convert.FromBase64String(variables.Get(SpecialVariables.Action.Azure.CertificateBytes));
            var webAppName = variables.Get(SpecialVariables.Action.Azure.WebAppName);

            Log.Info("Deploying to Azure WebApp '{0}' using subscription-id '{1}'", webAppName, subscriptionId);

            var publishProfile = GetPublishProfile(subscriptionId, webAppName, certificate);

            var changeSummary = DeploymentManager
                .CreateObject("contentPath", deployment.CurrentDirectory)
                .SyncTo("contentPath", 
                    BuildPath(publishProfile.MSDeploySite, variables), 
                    DeploymentOptions(publishProfile), 
                    DeploymentSyncOptions(variables));
            

            Log.Info("Successfully deployed to Azure. {0} objects added. {1} objects updated. {2} objects deleted.",
                changeSummary.ObjectsAdded, changeSummary.ObjectsUpdated, changeSummary.ObjectsDeleted);
        }

        private PublishProfile GetPublishProfile(string subscriptionId, string webAppName, byte[] certificate)
        {

            var cloudClient = CloudContext.Clients.CreateWebSiteManagementClient(
                new CertificateCloudCredentials(subscriptionId, new X509Certificate2(certificate)));

            var webApp = cloudClient.WebSpaces.List()
                .SelectMany(webSpace => cloudClient.WebSpaces.ListWebSites(webSpace.Name, new WebSiteListParameters { }))
                .FirstOrDefault(webSite => webSite.Name.Equals(webAppName, StringComparison.OrdinalIgnoreCase));

            if (webApp == null)
            {
                throw new CommandException(string.Format("Could not find Azure Web App '{0}' in subscription '{1}'",
                    webAppName, subscriptionId));
            }

            return cloudClient.WebSites.GetPublishProfile(webApp.WebSpace, webAppName)
                .PublishProfiles.First(x => x.PublishMethod.StartsWith("MSDeploy"));
        }

        private static string BuildPath(string site, VariableDictionary variables)
        {
            var relativePath = (variables.Get(SpecialVariables.Action.Azure.PhysicalPath) ?? "").TrimStart('\\');

            return relativePath != ""
                ? site + "\\" + relativePath
                : site;
        }

        private static DeploymentBaseOptions DeploymentOptions(PublishProfile publishProfile)
        {
            var options = new DeploymentBaseOptions
            {
                
                AuthenticationType = "Basic",
                RetryAttempts = 3,
                RetryInterval = 1000,
                TraceLevel = TraceLevel.Verbose,
                UserName = publishProfile.UserName,
                Password = publishProfile.UserPassword,
                UserAgent = "OctopusDeploy/1.0",
                ComputerName = string.Format("https://{0}/msdeploy.axd?site={1}", publishProfile.PublishUrl, publishProfile.MSDeploySite),
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
                DoNotDelete = !variables.GetFlag(SpecialVariables.Action.Azure.RemoveAdditionalFiles),
            };

            ApplyAppOfflineDeploymentRule(syncOptions, variables);
            ApplyPreserveAppDataDeploymentRule(syncOptions, variables);
            ApplyPreservePathsDeploymentRule(syncOptions, variables);
            return syncOptions;
        }

        private static void ApplyPreserveAppDataDeploymentRule(DeploymentSyncOptions syncOptions, VariableDictionary variables)
        {
            // If PreserveAppData variable set, then create SkipDelete rules for App_Data directory 
            if (variables.GetFlag(SpecialVariables.Action.Azure.PreserveAppData))
            {
                syncOptions.Rules.Add(new DeploymentSkipRule("SkipDeleteDataFiles", "Delete", "filePath", "\\\\App_Data\\\\.*", null));
                syncOptions.Rules.Add(new DeploymentSkipRule("SkipDeleteDataDir", "Delete", "dirPath", "\\\\App_Data(\\\\.*|$)", null));
            }
        }

        private static void ApplyPreservePathsDeploymentRule(DeploymentSyncOptions syncOptions, VariableDictionary variables)
        {
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
        }

        private static void ApplyAppOfflineDeploymentRule(DeploymentSyncOptions syncOptions, VariableDictionary variables)
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

    }

}