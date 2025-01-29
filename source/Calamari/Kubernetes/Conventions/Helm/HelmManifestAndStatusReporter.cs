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
                                       log.Warn($"Octopus needs Helm v3.13 or later to display object status and manifests. Your current version is {helmVersion}. Please update your Helm executable or container to enable our new Kubernetes capabilities. Learn more in our {log.FormatShortLink("KOS", "documentation")}.");
                                       return;
                                   }

                                   var manifest = await PollForManifest(deployment, releaseName, revisionNumber);

                                   //it's possible that we have no manifest as charts with just hooks don't produce a manifest
                                   //in this case, there is nothing to do, so we are done :)
                                   if (string.IsNullOrWhiteSpace(manifest))
                                   {
                                       return;
                                   }

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
            var didSuccessfullyExecuteCliCall = false;
            while (!ct.IsCancellationRequested)
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
                    await Task.Delay(TimeSpan.FromSeconds(1), ct.Token);
                }

            //it's possible that some manifests doesn't
            if (!didSuccessfullyExecuteCliCall)
                throw new CommandException("Failed to retrieve helm manifest in a timely manner");

            //Log if we found a manifest, or not
            log.Verbose(string.IsNullOrWhiteSpace(manifest)
                            ? $"Retrieved an empty manifest for {releaseName}, revision {revisionNumber}."
                            : $"Retrieved manifest for {releaseName}, revision {revisionNumber}.");

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

                    var @namespace = namespaceResolver.ResolveNamespace(rootNode, deployment.Variables);

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