using System;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Deployment.Conventions
{
    public interface IAwsAuthConventionFactory
    {
        IInstallConvention Create(Func<Task<bool>>? verifyLogin = null);
    }
}