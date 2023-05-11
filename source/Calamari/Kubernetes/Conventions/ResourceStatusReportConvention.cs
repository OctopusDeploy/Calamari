using System;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Deployment.Conventions;
using Calamari.Kubernetes.Integration;
using Calamari.Kubernetes.ResourceStatus;

namespace Calamari.Kubernetes.Conventions
{
    public class ResourceStatusReportConvention : IInstallConvention
    {
        private readonly ResourceStatusReportExecutor statusReportExecutor;
        private readonly ICommandLineRunner commandLineRunner;
        private readonly Func<Kubectl> kubectlFactory;

        public ResourceStatusReportConvention(ResourceStatusReportExecutor statusReportExecutor,
            ICommandLineRunner commandLineRunner, Func<Kubectl> kubectlFactory)
        {
            this.statusReportExecutor = statusReportExecutor;
            this.commandLineRunner = commandLineRunner;
            this.kubectlFactory = kubectlFactory;
        }

        public void Install(RunningDeployment deployment)
        {
            statusReportExecutor.ReportStatus(deployment.CurrentDirectory, commandLineRunner,
                deployment.EnvironmentVariables, kubectlFactory());
        }
    }
}