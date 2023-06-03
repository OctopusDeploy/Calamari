using System;
using Autofac;
using Calamari.Commands;
using Calamari.Common.Commands;

namespace Calamari.Deployment.Conventions
{
    public class InstallConventionFactory : IInstallConventionFactory
    {
        private readonly ILifetimeScope scope;

        public InstallConventionFactory(ILifetimeScope scope)
        {
            this.scope = scope;
        }

        public IInstallConvention Resolve<T>() where T : IInstallConvention
        {
            return scope.Resolve<T>();
        }

        public IInstallConvention ResolveDelegate(Action<RunningDeployment> convention)
        {
            return scope.Resolve<DelegateInstallConvention.Factory>().Invoke(convention);
        }
    }
}