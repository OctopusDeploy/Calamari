using System;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Deployment.Conventions;
using Calamari.Kubernetes.ResourceStatus;

namespace Calamari.Kubernetes.Conventions
{
    public class ResourceStatusReportConvention : IInstallConvention
    {
        private readonly ResourceStatusReportExecutor statusReportExecutor;
        private readonly ICommandLineRunner commandLineRunner;

        public ResourceStatusReportConvention(ResourceStatusReportExecutor statusReportExecutor, ICommandLineRunner commandLineRunner)
        {
            this.statusReportExecutor = statusReportExecutor;
            this.commandLineRunner = commandLineRunner;
        }

        public void Install(RunningDeployment deployment)
        {
            var successful = statusReportExecutor.ReportStatus(deployment.CurrentDirectory, commandLineRunner, deployment.EnvironmentVariables);
            if (!successful)
            {
                throw new TimeoutException("Not all resources have deployed successfully within timeout");
            }
        }
    }
}