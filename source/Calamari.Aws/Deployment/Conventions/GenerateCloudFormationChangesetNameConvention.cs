using System;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;

namespace Calamari.Aws.Deployment.Conventions
{
    public class GenerateCloudFormationChangesetNameConvention : IInstallConvention
    {
        public void Install(RunningDeployment deployment)
        {
            var name = $"octo-{Guid.NewGuid():N}";
            
            if (string.Compare(deployment.Variables[AwsSpecialVariables.CloudFormation.Changesets.Generate], "True", StringComparison.OrdinalIgnoreCase) == 0)
            {
                deployment.Variables.Set(AwsSpecialVariables.CloudFormation.Changesets.Name, name);
            }

            Log.SetOutputVariable("ChangesetName", name);
        }
    }
}