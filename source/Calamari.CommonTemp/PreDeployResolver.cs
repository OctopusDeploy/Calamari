using Autofac;

namespace Calamari.CommonTemp
{
    public class PreDeployResolver: Resolver<IPreDeployBehaviour>
    {
        public PreDeployResolver(ILifetimeScope lifetimeScope) : base(lifetimeScope)
        {
        }
    }
}