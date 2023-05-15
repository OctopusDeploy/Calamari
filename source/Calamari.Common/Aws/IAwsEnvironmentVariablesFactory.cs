using System;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Aws
{
    public interface IAwsEnvironmentVariablesFactory
    {
        IAwsEnvironmentVariables Create(ILog log, IVariables variables, Func<Task<bool>> verifyLogin = null);
    }
}