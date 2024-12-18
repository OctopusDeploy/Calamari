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
using Octopus.Versioning;
using Octopus.Versioning.Semver;
using YamlDotNet.RepresentationModel;

namespace Calamari.Kubernetes.Conventions.Helm
{
    public class HelmManifestAndStatusReporter
    {
        readonly ILog log;
        readonly IResourceStatusReportExecutor statusReporter;
        readonly IManifestReporter manifestReporter;
        readonly HelmCli helmCli;

        public HelmManifestAndStatusReporter(ILog log,
                                             IResourceStatusReportExecutor statusReporter,
                                             IManifestReporter manifestReporter,
                                             HelmCli helmCli)
        {
            this.log = log;
            this.statusReporter = statusReporter;
            this.manifestReporter = manifestReporter;
            this.helmCli = helmCli;
        }

        public async Task StartBackgroundMonitoringAndReporting(
            RunningDeployment deployment,
            string releaseName,
            int revisionNumber,
            CancellationToken cancellationToken)
        {
            await Task.Run(async () =>
                           {
                               var resourceStatusCheckIsEnabled = deployment.Variables.GetFlag(SpecialVariables.ResourceStatusCheck);
                      
                               if (
                                   resourceStatusCheckIsEnabled
                                   || FeatureToggle.KubernetesLiveObjectStatusFeatureToggle.IsEnabled(deployment.Variables)
                                   || OctopusFeatureToggles.KubernetesObjectManifestInspectionFeatureToggle.IsEnabled(deployment.Variables))
                               {
                                   if (!DoesHelmCliSupportManifestRetrieval(out var helmVersion))
                                   {
                                       log.Warn($"Helm manifest retrieval requires Helm v3.13 or later. Your current version is {helmVersion}. Please update your Helm executable or container.");
                                       return;
                                   }
                                   
                                   var manifest = await PollForManifest(deployment, releaseName, revisionNumber);

                                   //report the manifest has been applied
                                   manifestReporter.ReportManifestApplied(manifest);

                                   //if resource status (KOS) is enabled, parse the manifest and start monitoring the resources
                                   if (resourceStatusCheckIsEnabled)
                                   {
                                       await ParseManifestAndMonitorResourceStatuses(deployment, manifest, cancellationToken);
                                   }
                               }
                           },
                           cancellationToken);
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

        async Task<string> PollForManifest(RunningDeployment deployment,
                                           string releaseName,
                                           int revisionNumber)
        {
            var ct = new CancellationTokenSource();
            var timeout = GoDurationParser.TryParseDuration(deployment.Variables.Get(SpecialVariables.Helm.Timeout), out var timespan) ? timespan : TimeSpan.FromMinutes(5);
            ct.CancelAfter(timeout);
            string manifest = null;
            log.Verbose($"Retrieving manifest for {releaseName}, revision {revisionNumber}.");
            while (!ct.IsCancellationRequested)
                try
                {
                    manifest = helmCli.GetManifest(releaseName, revisionNumber);
                    log.Verbose($"Retrieved manifest for {releaseName}, revision {revisionNumber}.");
                    break;
                }
                catch (CommandLineException)
                {
                    log.Verbose($"Manifest could not be retrieved for {releaseName}, revision {revisionNumber}. Retrying in 1s.");
                    await Task.Delay(TimeSpan.FromSeconds(1), ct.Token);
                }

            if (string.IsNullOrWhiteSpace(manifest))
                throw new CommandException("Failed to retrieve helm manifest in a timely manner");

            return manifest;
        }

        async Task ParseManifestAndMonitorResourceStatuses(RunningDeployment deployment, string manifest, CancellationToken cancellationToken)
        {
            var resources = new List<ResourceIdentifier>();
            using (var reader = new StringReader(manifest))
            {
                var yamlStream = new YamlStream();
                yamlStream.Load(reader);

                foreach (var document in yamlStream.Documents)
                {
                    if (!(document.RootNode is YamlMappingNode rootNode))
                    {
                        if (document.RootNode.Tag != null)
                        {
                            log.Verbose("Could not parse manifest, resources will not be added to Kubernetes Object Status");
                        }
                        
                        continue;
                    }

                    var gvk = rootNode.ToResourceGroupVersionKind();

                    var metadataNode = rootNode.GetChildNode<YamlMappingNode>("metadata");
                    var name = metadataNode.GetChildNode<YamlScalarNode>("name").Value;
                    var @namespace = metadataNode.GetChildNodeIfExists<YamlScalarNode>("namespace")?.Value;

                    //if the resource doesn't have a namespace set in the manifest set it to the helm namespace.
                    //This is because namespaced resources that don't have the namespace defined in the manifest will be set in the namespace set in the helm command
                    //if this is null, we'll fall back on the namespace defined for the kubectl tool (which is the default target namespace)
                    //we aren't changing the manifest here, just changing where the kubectl looks for our resource.
                    //We also try and filter out known non-namespaced resources
                    if (string.IsNullOrWhiteSpace(@namespace) && !KubernetesApiResources.NonNamespacedKinds.Contains(gvk.Kind))
                        @namespace = deployment.Variables.Get(SpecialVariables.Helm.Namespace)?.Trim();

                    var resourceIdentifier = new ResourceIdentifier(gvk, name, @namespace);
                    resources.Add(resourceIdentifier);
                }
            }

            //We are using helm as the deployment verification so an infinite timeout and wait for jobs makes sense
            var statusCheck = statusReporter.Start(0, false, resources);
            await statusCheck.WaitForCompletionOrTimeout(cancellationToken);
        }
    }
}