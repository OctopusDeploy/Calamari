using System;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.Conventions;

namespace Calamari.Aws.Integration
{
    public class AwsAuthConventionFactory : IAwsAuthConventionFactory
    {
        public IInstallConvention Create(ILog log, IVariables variables, Func<Task<bool>> verifyLogin = null)
        {
            return new AwsAuthConvention(log, variables, verifyLogin);
        }
    }
}