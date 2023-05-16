using System;
using System.Threading.Tasks;
using Calamari.CloudAccounts;
using Calamari.Common.Aws;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Aws.Integration
{
    public class AwsEnvironmentVariablesGenerator : IAwsEnvironmentVariablesGenerator
    {
        public IAwsEnvironmentVariables Create(ILog log, IVariables variables, Func<Task<bool>> verifyLogin = null)
        {
            return AwsEnvironmentGeneration.Create(log, variables, verifyLogin).GetAwaiter().GetResult();
        }
    }
}