using Autofac;

namespace Calamari.CommonTemp
{
    public class PostDeployResolver: Resolver<IPostDeployBehaviour>
    {
        public PostDeployResolver(ILifetimeScope lifetimeScope) : base(lifetimeScope)
        {
        }
    }
}