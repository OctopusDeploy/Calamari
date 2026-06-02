using System;
using System.Threading.Tasks;
using Calamari.CloudAccounts;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.Conventions;

namespace Calamari.Aws.Integration;

public class AwsAuthConvention(ILog log, IVariables variables, Func<Task<bool>> verifyLogin = null) : IInstallConvention
{
    public void Install(RunningDeployment deployment)
    {
        var awsEnvironmentVars = AwsEnvironmentGeneration.Create(log, variables, verifyLogin).GetAwaiter().GetResult();
        foreach (var envVar in awsEnvironmentVars.EnvironmentVars)
        {
            deployment.EnvironmentVariables[envVar.Key] = envVar.Value;
        }
    }
}