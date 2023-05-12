using Calamari.CloudAccounts;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;

namespace Calamari.Aws.Integration
{
    public class AwsAuthConvention : IInstallConvention
    {
        private readonly ILog log;

        public AwsAuthConvention(ILog log)
        {
            this.log = log;
        }

        public void Install(RunningDeployment deployment)
        {
            if (deployment.Variables.Get(SpecialVariables.Account.AccountType) == "AmazonWebServicesAccount")
            {
                var awsEnvironmentVars = AwsEnvironmentGeneration.Create(log, deployment.Variables).GetAwaiter().GetResult().EnvironmentVars;
                foreach (var envVar in awsEnvironmentVars)
                {
                    deployment.EnvironmentVariables[envVar.Key] = envVar.Value;
                }
            }
        }
    }
}