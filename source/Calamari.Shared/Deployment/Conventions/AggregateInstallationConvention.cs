using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Commands;

namespace Calamari.Deployment.Conventions
{
    public class AggregateInstallationConvention: IInstallConvention
    {
        readonly IInstallConvention[] conventions;

        public AggregateInstallationConvention(params IInstallConvention[] conventions)
        {
            this.conventions = conventions;
        }

        public AggregateInstallationConvention(IEnumerable<IInstallConvention> conventions)
        {
            this.conventions = conventions?.ToArray() ?? new IInstallConvention[0];
        }

        public void Install(RunningDeployment deployment)
        {
            foreach (var convention in conventions)
            {
                convention.Install(deployment);
                if (deployment.Variables.GetFlag(Common.Plumbing.Variables.KnownVariables.Action.SkipRemainingConventions))
                {
                    break;
                }
            }
        }
    }
}