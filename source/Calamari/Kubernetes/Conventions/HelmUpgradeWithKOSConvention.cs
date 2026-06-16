using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.Conventions;
using Calamari.Kubernetes.Conventions.Helm;
using Calamari.Kubernetes.Helm;
using Calamari.Kubernetes.Integration;
using Calamari.Kubernetes.ResourceStatus;

namespace Calamari.Kubernetes.Conventions
{
    public class HelmUpgradeWithKOSConvention : IInstallConvention
    {
        readonly ILog log;
        readonly ICommandLineRunner commandLineRunner;
        readonly ICalamariFileSystem fileSystem;
        readonly HelmTemplateValueSourcesParser valueSourcesParser;
        readonly IResourceStatusReportExecutor statusReporter;
        readonly IManifestReporter manifestReporter;
        readonly IKubernetesManifestNamespaceResolver namespaceResolver;
        readonly Kubectl kubectl;

        public HelmUpgradeWithKOSConvention(ILog log,
                                            ICommandLineRunner commandLineRunner,
                                            ICalamariFileSystem fileSystem,
                                            HelmTemplateValueSourcesParser valueSourcesParser,
                                            IResourceStatusReportExecutor statusReporter,
                                            IManifestReporter manifestReporter,
                                            IKubernetesManifestNamespaceResolver namespaceResolver,
                                            Kubectl kubectl)
        {
            this.log = log;
            this.commandLineRunner = commandLineRunner;
            this.fileSystem = fileSystem;
            this.valueSourcesParser = valueSourcesParser;
            this.statusReporter = statusReporter;
            this.manifestReporter = manifestReporter;
            this.namespaceResolver = namespaceResolver;
            this.kubectl = kubectl;
        }

        public void Install(RunningDeployment deployment)
        {
            var releaseName = GetReleaseName(deployment.Variables);

            var helmCli = new HelmCli(log, commandLineRunner, deployment, fileSystem);

            kubectl.SetKubectl();

            var currentRevisionNumber = helmCli.GetCurrentRevision(releaseName);

            var newRevisionNumber = (currentRevisionNumber ?? 0) + 1;

            //When ArgoRollouts support is enabled, the parallel manifest + KOS reporter is replaced
            //by a separate verification action that runs after the deploy step. Manifest reporting
            //and AppliedResources emission are performed inline by HelmUpgradeExecutor instead.
            if (OctopusFeatureToggles.ArgoRolloutsSupportFeatureToggle.IsEnabled(deployment.Variables))
            {
                var executor = new HelmUpgradeExecutor(log, fileSystem, valueSourcesParser, helmCli, namespaceResolver, manifestReporter);
                executor.ExecuteHelmUpgrade(deployment, releaseName, newRevisionNumber, new CancellationTokenSource(), new CancellationTokenSource());
                return;
            }

            //This is used to cancel KOS when the helm upgrade has completed
            //It does not cancel the get manifest
            var helmInstallCompletedCts = new CancellationTokenSource();

            //This is used to cancel the get manifest when the helm install fails (and we are still trying to retrieve the manifest)
            var helmInstallErrorCts = new CancellationTokenSource();

            var helmUpgradeTask = Task.Run(() =>
                                           {
                                               var executor = new HelmUpgradeExecutor(log,
                                                                                      fileSystem,
                                                                                      valueSourcesParser,
                                                                                      helmCli,
                                                                                      namespaceResolver);

                                               executor.ExecuteHelmUpgrade(deployment, releaseName, newRevisionNumber, helmInstallCompletedCts, helmInstallErrorCts);
                                           });

            var manifestAndStatusCheckTask = Task.Run(async () =>
                                                      {
                                                          var runner = new HelmManifestAndStatusReporter(log, statusReporter, manifestReporter, namespaceResolver, helmCli);

                                                          await runner.StartBackgroundMonitoringAndReporting(deployment,
                                                                               releaseName,
                                                                               newRevisionNumber,
                                                                               helmInstallCompletedCts.Token,
                                                                               helmInstallErrorCts.Token);
                                                      },
                                                      helmInstallCompletedCts.Token);

            //we run both the helm upgrade and the manifest & status in parallel
            Task.WhenAll(helmUpgradeTask, manifestAndStatusCheckTask).GetAwaiter().GetResult();
        }

        string GetReleaseName(IVariables variables)
        {
            var validChars = new Regex("[^a-zA-Z0-9-]");
            var releaseName = variables.Get(SpecialVariables.Helm.ReleaseName)?.ToLower();
            if (string.IsNullOrWhiteSpace(releaseName))
            {
                releaseName = $"{variables.Get(ActionVariables.Name)}-{variables.Get(DeploymentEnvironment.Name)}";
                releaseName = validChars.Replace(releaseName, "").ToLowerInvariant();
            }

            log.SetOutputVariable("ReleaseName", releaseName, variables);
            log.Info($"Using Release Name {releaseName}");
            return releaseName;
        }
    }
}