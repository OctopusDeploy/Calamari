using System;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.Conventions;

namespace Calamari.Kubernetes
{
    public class AwsAuthConventionFactoryFactory
    {
        private readonly IAwsAuthConventionFactory awsEnvironmentVariablesGenerator;

        public AwsAuthConventionFactoryFactory(IAwsAuthConventionFactory awsEnvironmentVariablesGenerator)
        {
            this.awsEnvironmentVariablesGenerator = awsEnvironmentVariablesGenerator;
        }

        public IInstallConvention Create(ILog log, IVariables variables)
        {
            return awsEnvironmentVariablesGenerator.Create(log, variables);
        }
    }
}