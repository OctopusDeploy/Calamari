using System;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.Conventions;

namespace Calamari.Kubernetes
{
    public class AwsAuthConventionFactoryFactory
    {
        private readonly Lazy<IAwsAuthConventionFactory> awsEnvironmentVariablesGeneratorLazy;

        public AwsAuthConventionFactoryFactory(Lazy<IAwsAuthConventionFactory> awsEnvironmentVariablesGeneratorLazy)
        {
            this.awsEnvironmentVariablesGeneratorLazy = awsEnvironmentVariablesGeneratorLazy;
        }

        public IInstallConvention Create(ILog log, IVariables variables)
        {
            return awsEnvironmentVariablesGeneratorLazy.Value.Create(log, variables);
        }
    }
}