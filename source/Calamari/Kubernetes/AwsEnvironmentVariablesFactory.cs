using System;
using Calamari.Common.Aws;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Kubernetes
{
    public class AwsEnvironmentVariablesFactory
    {
        private readonly Lazy<IAwsEnvironmentVariablesGenerator> awsEnvironmentVariablesGeneratorLazy;

        public AwsEnvironmentVariablesFactory(Lazy<IAwsEnvironmentVariablesGenerator> awsEnvironmentVariablesGeneratorLazy)
        {
            this.awsEnvironmentVariablesGeneratorLazy = awsEnvironmentVariablesGeneratorLazy;
        }

        public IAwsEnvironmentVariables Create(ILog log, IVariables variables)
        {
            return awsEnvironmentVariablesGeneratorLazy.Value.Create(log, variables);
        }
    }
}