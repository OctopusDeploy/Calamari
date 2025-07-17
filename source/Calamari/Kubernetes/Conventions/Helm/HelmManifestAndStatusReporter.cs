using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.Logging;
using Calamari.Kubernetes.Integration;
using Calamari.Kubernetes.ResourceStatus;
using Calamari.Kubernetes.ResourceStatus.Resources;
using Calamari.Util;
using Octopus.Versioning.Semver;
using YamlDotNet.RepresentationModel;

namespace Calamari.Kubernetes.Conventions.Helm
{
    public class HelmManifestAndStatusReporter
    {
        readonly ILog log;
        readonly IResourceStatusReportExecutor statusReporter;
        readonly IManifestReporter manifestReporter;
        readonly IKubernetesManifestNamespaceResolver namespaceResolver;
        readonly HelmCli helmCli;

        public HelmManifestAndStatusReporter(ILog log,
                                             IResourceStatusReportExecutor statusReporter,
                                             IManifestReporter manifestReporter,
                                             IKubernetesManifestNamespaceResolver namespaceResolver,
                                             HelmCli helmCli)
        {
            this.log = log;
            this.statusReporter = statusReporter;
            this.manifestReporter = manifestReporter;
            this.namespaceResolver = namespaceResolver;
            this.helmCli = helmCli;
        }

        public async Task StartBackgroundMonitoringAndReporting(RunningDeployment deployment,
                                                                string releaseName,
                                                                int revisionNumber,
                                                                CancellationToken helmInstallCompletedCancellationToken,
                                                                CancellationToken helmInstallErrorCancellationToken)
        {
            await Task.Run(async () =>
                           {
                               var resourceStatusCheckIsEnabled = deployment.Variables.GetFlag(SpecialVariables.ResourceStatusCheck);

                               if (
                                   resourceStatusCheckIsEnabled
                                   || FeatureToggle.KubernetesLiveObjectStatusFeatureToggle.IsEnabled(deployment.Variables)
                                   || OctopusFeatureToggles.KubernetesObjectManifestInspectionFeatureToggle.IsEnabled(deployment.Variables))
                               {
                                   if (!DeploymentSupportsManifestReporting(deployment, out var reason))
                                   {
                                       log.Verbose(reason);
                                       return;
                                   }

                                   if (!DoesHelmCliSupportManifestRetrieval(out var helmVersion))
                                   {
                                       log.Warn($"Octopus needs Helm v3.13 or later to display object status and manifests. Your current version is {helmVersion}. Please update your Helm executable or container to enable our new Kubernetes capabilities. Learn more in our {log.FormatShortLink("KOS", "documentation")}.");
                                       return;
                                   }

                                   var manifest = await PollForManifest(deployment, releaseName, revisionNumber, helmInstallErrorCancellationToken);

                                   //it's possible that we have no manifest as charts with just hooks don't produce a manifest
                                   //in this case, there is nothing to do, so we are done :)
                                   if (string.IsNullOrWhiteSpace(manifest))
                                   {
                                       return;
                                   }

                                   //report the manifest has been applied
                                   manifestReporter.ReportManifestApplied(manifest);

                                   //we want to cancel KOS if either token is cancelled
                                   var kosKts = CancellationTokenSource.CreateLinkedTokenSource(helmInstallCompletedCancellationToken, helmInstallErrorCancellationToken);

                                   //if resource status (KOS) is enabled, parse the manifest and start monitoring the resources
                                   if (resourceStatusCheckIsEnabled)
                                   {
                                       await ParseManifestAndMonitorResourceStatuses(deployment, manifest, kosKts.Token);
                                   }
                               }
                           },
                           helmInstallCompletedCancellationToken);
        }

        static readonly SemanticVersion MinimumHelmVersion = new SemanticVersion(3, 13, 0);

        bool DoesHelmCliSupportManifestRetrieval(out string helmVersion)
        {
            var parsedExecutableVersion = helmCli.GetParsedExecutableVersion();

            if (parsedExecutableVersion == null)
            {
                helmVersion = "UNKNOWN";
                return false;
            }

            helmVersion = parsedExecutableVersion.Version.ToString();
            return parsedExecutableVersion >= MinimumHelmVersion;
        }

        static bool DeploymentSupportsManifestReporting(RunningDeployment deployment, out string reason)
        {
            var additionalArguments = deployment.Variables.Get(SpecialVariables.Helm.AdditionalArguments);
            if (additionalArguments?.Contains("--dry-run") ?? false)
            {
                reason = "Helm --dry-run is enabled, no object statuses will be reported";
                return false;
            }

            reason = "";
            return true;
        }

        async Task<string> PollForManifest(RunningDeployment deployment,
                                           string releaseName,
                                           int revisionNumber,
                                           CancellationToken helmInstallErrorCancellationToken)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(helmInstallErrorCancellationToken);
            var timeout = GoDurationParser.TryParseDuration(deployment.Variables.Get(SpecialVariables.Helm.Timeout), out var timespan) ? timespan : TimeSpan.FromMinutes(5);
            cts.CancelAfter(timeout);
            string manifest = null;
            log.Verbose($"Retrieving manifest for {releaseName}, revision {revisionNumber}.");
            var didSuccessfullyExecuteCliCall = false;

            while (!cts.IsCancellationRequested)
            {
                try
                {
                    manifest = helmCli.GetManifest(releaseName, revisionNumber);

                    //flag if we successfully executed the get manifest call
                    //this is important because some helm charts just have hooks that don't have any manifests, so we receive null/empty string here
                    didSuccessfullyExecuteCliCall = true;
                    break;
                }
                catch (CommandLineException)
                {
                    log.Verbose($"Manifest could not be retrieved for {releaseName}, revision {revisionNumber}. Retrying in 1s.");
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1), cts.Token);
                    }
                    catch (TaskCanceledException)
                    {
                        //We don't care if the delay was cancelled
                    }
                }
            }

            //if the helm install failed
            if (helmInstallErrorCancellationToken.IsCancellationRequested)
            {
                log.Verbose("Canceling manifest retrieval as Helm installation failed");
                return null;
            }

            //If we can't retrieve the manifest
            if (!didSuccessfullyExecuteCliCall)
            {
                throw new CommandException("Failed to retrieve Helm manifest in a timely manner");
            }

            //Log if we found a manifest, or not
            log.Verbose(string.IsNullOrWhiteSpace(manifest)
                            ? $"Retrieved an empty manifest for {releaseName}, revision {revisionNumber}."
                            : $"Retrieved manifest for {releaseName}, revision {revisionNumber}.");

            return manifest;
        }

        async Task ParseManifestAndMonitorResourceStatuses(RunningDeployment deployment, string manifest, CancellationToken cancellationToken)
        {
            var resources = ManifestParser.GetResourcesFromManifest(manifest, namespaceResolver, deployment.Variables, log);

            //We are using helm as the deployment verification so an infinite timeout and wait for jobs makes sense
            var statusCheck = statusReporter.Start(0, false, resources);
            await statusCheck.WaitForCompletionOrTimeout(cancellationToken);
        }
    }
}