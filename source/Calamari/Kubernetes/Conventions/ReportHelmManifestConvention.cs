using System;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.Logging;
using Calamari.Deployment.Conventions;
using Calamari.Kubernetes.Integration;

namespace Calamari.Kubernetes.Conventions
{
    public class ReportHelmManifestConvention : IInstallConvention
    {
        readonly ILog log;
        readonly ICommandLineRunner commandLineRunner;
        readonly IManifestReporter manifestReporter;

        public ReportHelmManifestConvention(ILog log,
                                            ICommandLineRunner commandLineRunner,
                                            IManifestReporter manifestReporter)
        {
            this.log = log;
            this.commandLineRunner = commandLineRunner;
            this.manifestReporter = manifestReporter;
        }

        public void Install(RunningDeployment deployment)
        {
            if (!FeatureToggle.KubernetesLiveObjectStatusFeatureToggle.IsEnabled(deployment.Variables)
                && !OctopusFeatureToggles.KubernetesObjectManifestInspectionFeatureToggle.IsEnabled(deployment.Variables))
                return;

            var releaseName = deployment.Variables.Get("ReleaseName");

            var helm = new HelmCli(log, commandLineRunner, deployment.CurrentDirectory, deployment.EnvironmentVariables)
                       .WithExecutable(deployment.Variables)
                       .WithNamespace(deployment.Variables);

            var manifest = helm.GetManifest(releaseName, 1);
            manifestReporter.ReportManifestApplied(manifest);
        }
    }
}