using System;
using System.Threading.Tasks;
using Calamari.Common.Aws;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.CloudAccounts
{
    public class AwsEnvironmentVariablesFactory : IAwsEnvironmentVariablesFactory
    {
        public async Task<IAwsEnvironmentVariables> Create(ILog log, IVariables variables, Func<Task<bool>> verifyLogin = null)
        {
            return await AwsEnvironmentGeneration.Create(log, variables, verifyLogin);
        }
    }
}