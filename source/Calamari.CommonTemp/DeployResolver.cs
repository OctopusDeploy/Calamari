using Autofac;

namespace Calamari.CommonTemp
{
    public class DeployResolver: Resolver<IDeployBehaviour>
    {
        public DeployResolver(ILifetimeScope lifetimeScope) : base(lifetimeScope)
        {
        }
    }
}