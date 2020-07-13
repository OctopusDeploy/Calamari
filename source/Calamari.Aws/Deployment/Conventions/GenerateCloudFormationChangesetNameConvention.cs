using System;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;

namespace Calamari.Aws.Deployment.Conventions
{
    public class GenerateCloudFormationChangesetNameConvention : IInstallConvention
    {
        readonly ILog log;

        public GenerateCloudFormationChangesetNameConvention(ILog log)
        {
            this.log = log;
        }
        
        public void Install(RunningDeployment deployment)
        {
            var name = $"octo-{Guid.NewGuid():N}";
            
            if (string.Compare(deployment.Variables[AwsSpecialVariables.CloudFormation.Changesets.Generate], "True", StringComparison.OrdinalIgnoreCase) == 0)
            {
                deployment.Variables.Set(AwsSpecialVariables.CloudFormation.Changesets.Name, name);
            }

            log.SetOutputVariableButDoNotAddToVariables("ChangesetName", name);
        }
    }
}