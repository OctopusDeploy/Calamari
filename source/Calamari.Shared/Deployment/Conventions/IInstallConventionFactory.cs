using System;
using Calamari.Common.Commands;

namespace Calamari.Deployment.Conventions
{
    public interface IInstallConventionFactory
    {
        IInstallConvention Resolve<T>() where T : IInstallConvention;

        IInstallConvention ResolveDelegate(Action<RunningDeployment> convention);
    }
}