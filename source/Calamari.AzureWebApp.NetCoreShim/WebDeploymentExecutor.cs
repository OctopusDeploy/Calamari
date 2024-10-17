using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Retry;
using Microsoft.Web.Deployment;
using Newtonsoft.Json;
using Serilog;

namespace Calamari.AzureWebApp.NetCoreShim
{
    public class WebDeploymentExecutor
    {
        static readonly LimitedExponentialRetryInterval RetryIntervalForAzureOperations = new LimitedExponentialRetryInterval(5000, 30000, 2);
        readonly ILogger logger;

        public WebDeploymentExecutor(ILogger logger)
        {
            this.logger = logger;
        }

        public async Task Execute(Program.SyncOptions options)
        {
            var retry = new RetryTracker(4,
                                         timeLimit: TimeSpan.MaxValue,
                                         retryInterval: RetryIntervalForAzureOperations);
            while (retry.Try())
            {
                try
                {
                    var changeSummary = DeploymentManager
                                        .CreateObject("contentPath", options.SourceContentPath)
                                        .SyncTo(
                                                "contentPath",
                                                options.DestinationContentPath,
                                                DeploymentOptions(options),
                                                DeploymentSyncOptions(options)
                                               );

                    var resultJson = JsonConvert.SerializeObject(new
                                                                 {
                                                                     changeSummary.ObjectsAdded,
                                                                     changeSummary.ObjectsUpdated,
                                                                     changeSummary.ObjectsDeleted
                                                                 },
                                                                 Formatting.None);

                    logger.Information("RESULT|{JSON:l}", resultJson);

                    //big success!
                    break;
                }
                catch (DeploymentDetailedException ex)
                {
                    if (retry.CanRetry())
                    {
                        if (retry.ShouldLogWarning())
                        {
                            logger.Verbose("Retry #{Count} on Azure deploy. Exception: {Message}", retry.CurrentTry, ex.Message);
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

        DeploymentBaseOptions DeploymentOptions(Program.SyncOptions options)
        {
            var encryption = new AesEncryption(options.EncryptionKey);
            var decryptedUserName = encryption.Decrypt(Convert.FromBase64String(options.DestinationUserName));
            var decryptedPassword = encryption.Decrypt(Convert.FromBase64String(options.DestinationPassword));

            var deploymentOptions = new DeploymentBaseOptions
            {
                AuthenticationType = "Basic",
                RetryAttempts = 3,
                RetryInterval = 1000,
                TraceLevel = TraceLevel.Verbose,
                UserName = decryptedUserName,
                Password = decryptedPassword,
                UserAgent = "OctopusDeploy/1.0",
                ComputerName = new Uri(options.DestinationUri, $"/msdeploy.axd?site={options.DestinationDeploymentSite}").ToString()
            };

            deploymentOptions.Trace += (sender, args) =>
                                       {
                                           switch (args.EventLevel)
                                           {
                                               case TraceLevel.Verbose:
                                                   logger.Verbose(args.Message);
                                                   break;
                                               case TraceLevel.Info:
                                                   // The deploy-log is noisy; we'll log info as verbose
                                                   logger.Verbose(args.Message);
                                                   break;
                                               case TraceLevel.Warning:
                                                   logger.Warning(args.Message);
                                                   break;
                                               case TraceLevel.Error:
                                                   logger.Error(args.Message);
                                                   break;
                                           }
                                       };

            return deploymentOptions;
        }

        DeploymentSyncOptions DeploymentSyncOptions(Program.SyncOptions options)
        {
            var syncOptions = new DeploymentSyncOptions
            {
                WhatIf = false,
                UseChecksum = options.UseChecksum,
                DoNotDelete = options.DoNotDelete,
            };

            if (options.UseAppOffline)
            {
                var rules = Microsoft.Web.Deployment.DeploymentSyncOptions.GetAvailableRules();
                if (rules.TryGetValue("AppOffline", out var rule))
                {
                    syncOptions.Rules.Add(rule);
                }
                else
                {
                    logger.Verbose("Azure Deployment API does not support `AppOffline` deployment rule.");
                }
            }

            ApplyPreserveAppDataDeploymentRule(syncOptions, options);
            ApplyPreservePathsDeploymentRule(syncOptions, options);
            return syncOptions;
        }

        static void ApplyPreserveAppDataDeploymentRule(DeploymentSyncOptions syncOptions, Program.SyncOptions options)
        {
            if (options.DoPreserveAppData)
            {
                syncOptions.Rules.Add(new DeploymentSkipRule("SkipDeleteDataFiles",
                                                             "Delete",
                                                             "filePath",
                                                             @"\\App_Data\\.*",
                                                             null));
                syncOptions.Rules.Add(new DeploymentSkipRule("SkipDeleteDataDir",
                                                             "Delete",
                                                             "dirPath",
                                                             @"\\App_Data(\\.*|$)",
                                                             null));
            }
        }

        static void ApplyPreservePathsDeploymentRule(DeploymentSyncOptions syncOptions, Program.SyncOptions options)
        {
            var preservePaths = options.PreservePaths.ToList();
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
    }
}