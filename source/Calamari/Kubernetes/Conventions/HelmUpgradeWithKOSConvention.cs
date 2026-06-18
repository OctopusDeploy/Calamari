using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
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

            // GetCurrentRevision returns null when the release doesn't exist yet; in that case
            // there's nothing to recover from, so we skip the status check entirely.
            var currentRevisionNumber = helmCli.GetCurrentRevision(releaseName);
            if (currentRevisionNumber != null && CheckAndHandleStuckRelease(helmCli, releaseName))
            {
                // Re-read revision after recovery so newRevisionNumber reflects the post-rollback state.
                // Skipped on the happy path (no recovery ran) since the revision cannot have changed.
                currentRevisionNumber = helmCli.GetCurrentRevision(releaseName);
            }

            var newRevisionNumber = (currentRevisionNumber ?? 0) + 1;

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

        // Returns true if a recovery action was attempted (indicating the revision number may have changed).
        bool CheckAndHandleStuckRelease(HelmCli helmCli, string releaseName)
        {
            var status = helmCli.GetReleaseStatus(releaseName);

            if (status == null)
                return false;

            log.Info($"Release {releaseName} current status: {status}");

            // Handle problematic states that could be left from cancelled deployments
            switch (status.ToLowerInvariant())
            {
                case "pending-install":
                    // No prior successful revision exists, so rollback is not possible. Uninstall the
                    // stuck release so the next upgrade --install can start cleanly.
                    log.Warn($"Release {releaseName} is stuck in {status} state, likely from a cancelled first install. Uninstalling to recover...");
                    try
                    {
                        var uninstallResult = helmCli.Uninstall(releaseName);
                        if (uninstallResult.ExitCode == 0)
                            log.Info($"Successfully uninstalled stuck release {releaseName}");
                        else
                            log.Warn($"Uninstall had non-zero exit code but continuing: {uninstallResult.ExitCode}");
                    }
                    catch (Exception ex)
                    {
                        log.Warn($"Failed to uninstall release {releaseName}: {ex.Message}. Continuing with deployment...");
                    }
                    return true;

                case "pending-upgrade":
                    log.Warn($"Release {releaseName} is stuck in {status} state, likely from a cancelled deployment. Rolling back to recover...");
                    try
                    {
                        var rollbackResult = helmCli.Rollback(releaseName);
                        if (rollbackResult.ExitCode == 0)
                            log.Info($"Successfully rolled back release {releaseName}");
                        else
                            log.Warn($"Rollback had non-zero exit code but continuing: {rollbackResult.ExitCode}");
                    }
                    catch (Exception ex)
                    {
                        log.Warn($"Failed to rollback release {releaseName}: {ex.Message}. Continuing with deployment...");
                    }
                    return true;

                case "failed":
                    log.Info($"Release {releaseName} is in failed state. Helm upgrade --install should handle this automatically.");
                    return false;

                default:
                    log.Verbose($"Release {releaseName} status: {status} - proceeding with deployment");
                    return false;
            }
        }
    }
}