using System;
using Calamari.Common.Commands;

namespace Calamari.Deployment.Conventions
{
    public class ConditionalInstallationConvention<T>: IInstallConvention where T: IInstallConvention
    {
        private readonly Func<RunningDeployment, bool> predicate;

        public ConditionalInstallationConvention(Func<RunningDeployment, bool> predicate, T decorated)
        {
            Decorated = decorated;
            this.predicate = predicate;
        }

        protected T Decorated { get; }

        public void Install(RunningDeployment deployment)
        {
            if (predicate(deployment))
            {
                Decorated.Install(deployment);
            }
        }
    }
}
