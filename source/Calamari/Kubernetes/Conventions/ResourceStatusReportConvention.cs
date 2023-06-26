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

        public ResourceStatusReportConvention(ResourceStatusReportExecutor statusReportExecutor)
        {
            this.statusReportExecutor = statusReportExecutor;
        }

        public void Install(RunningDeployment deployment)
        {
            statusReportExecutor.ReportStatus(deployment.CurrentDirectory);
        }
    }
}