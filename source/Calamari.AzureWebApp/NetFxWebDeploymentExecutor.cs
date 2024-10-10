#if NETFRAMEWORK
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Calamari.Azure.AppServices;
using Calamari.AzureWebApp.Integration.Websites.Publishing;
using Calamari.AzureWebApp.Util;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Microsoft.Web.Deployment;

namespace Calamari.AzureWebApp
{
    public class NetFxWebDeploymentExecutor : IWebDeploymentExecutor
    {
        readonly ILog log;

        public NetFxWebDeploymentExecutor(ILog log)
        {
            this.log = log;
        }
        
        public async Task ExecuteDeployment(RunningDeployment deployment, AzureTargetSite targetSite, IVariables variables, WebDeployPublishSettings publishSettings)
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

        static string BuildPath(AzureTargetSite site, IVariables variables)
        {
            var relativePath = variables.Get(SpecialVariables.Action.Azure.PhysicalPath, String.Empty)?.TrimStart('\\');
            return !string.IsNullOrWhiteSpace(relativePath)
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

        static void ApplyPreserveAppDataDeploymentRule(DeploymentSyncOptions syncOptions,
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

        static void ApplyPreservePathsDeploymentRule(DeploymentSyncOptions syncOptions,
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
#endif