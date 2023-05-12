using System;
using Calamari.Common.Commands;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;

namespace Calamari.Commands
{
    public class DelegateInstallConvention : IInstallConvention
    {
        public delegate DelegateInstallConvention Factory(Action<RunningDeployment> convention);

        readonly Action<RunningDeployment> convention;

        public DelegateInstallConvention(Action<RunningDeployment> convention)
        {
            this.convention = convention;
        }

        public void Install(RunningDeployment deployment)
            => convention(deployment);
    }
}
