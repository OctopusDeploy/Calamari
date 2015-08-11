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
            var certificate = Convert.FromBase64String(variables.Get(SpecialVariables.Action.Azure.CertificateBytes));
            var webAppName = variables.Get(SpecialVariables.Action.Azure.WebAppName);

            Log.Info("Deploying to Azure WebApp '{0}' using subscription-id '{1}'", webAppName, subscriptionId);

            var cloudClient = CloudContext.Clients.CreateWebSiteManagementClient( 
                new CertificateCloudCredentials(subscriptionId, new X509Certificate2(certificate)));

            var webApp = cloudClient.WebSpaces.List()
                .SelectMany(webSpace => cloudClient.WebSpaces.ListWebSites(webSpace.Name, new WebSiteListParameters {}))
                .FirstOrDefault(webSite => webSite.Name.Equals(webAppName, StringComparison.OrdinalIgnoreCase));

            if (webApp == null)
            {
               throw new CommandException(string.Format("Could not find Azure Web App '{0}' in subscription '{1}'",
                   webAppName, subscriptionId)); 
            }

            var publishProfile = cloudClient.WebSites.GetPublishProfile(webApp.WebSpace, webAppName)
                .PublishProfiles.First(x => x.PublishMethod.StartsWith("MSDeploy"));

            var changeSummary = DeploymentManager
                .CreateObject("contentPath", deployment.CurrentDirectory)
                .SyncTo("contentPath", publishProfile.MSDeploySite, DeploymentOptions(publishProfile), 
                new DeploymentSyncOptions {WhatIf = false, UseChecksum = true});

            Log.Info("Successfully deployed to Azure. {0} objects added. {1} objects updated. {2} objects deleted.",
                changeSummary.ObjectsAdded, changeSummary.ObjectsUpdated, changeSummary.ObjectsDeleted);
        }

        private static DeploymentBaseOptions DeploymentOptions(
            WebSiteGetPublishProfileResponse.PublishProfile publishProfile)
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