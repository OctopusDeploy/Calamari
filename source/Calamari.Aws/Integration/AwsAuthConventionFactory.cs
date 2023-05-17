using System;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.Conventions;

namespace Calamari.Aws.Integration
{
    public class AwsAuthConventionFactory : IAwsAuthConventionFactory
    {
        private readonly AwsAuthConvention.Factory awsAuthConventionFactory;

        public AwsAuthConventionFactory(AwsAuthConvention.Factory awsAuthConventionFactory)
        {
            this.awsAuthConventionFactory = awsAuthConventionFactory;
        }

        public IInstallConvention Create(Func<Task<bool>> verifyLogin = null)
        {
            return awsAuthConventionFactory(verifyLogin);
        }
    }
}