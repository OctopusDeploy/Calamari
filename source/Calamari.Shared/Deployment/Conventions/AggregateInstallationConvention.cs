using System.Collections.Generic;
using System.Linq;

namespace Calamari.Deployment.Conventions
{
    public class AggregateInstallationConvention: IInstallConvention
    {
        private readonly IInstallConvention[] conventions;

        public AggregateInstallationConvention(params IInstallConvention[] conventions)
        {
            this.conventions = conventions;
        }
        
        public AggregateInstallationConvention(IEnumerable<IInstallConvention> conventions)
        {
            conventions = conventions?.ToArray() ?? new IInstallConvention[0];
        }
        
        public void Install(RunningDeployment deployment)
        {
            foreach (var convention in conventions)
            {
                convention.Install(deployment);
                if (deployment.Variables.GetFlag(SpecialVariables.Action.SkipRemainingConventions))
                {
                    break;
                }
            }
        }
    }
}