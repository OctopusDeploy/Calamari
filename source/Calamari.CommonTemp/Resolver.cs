using Autofac;

namespace Calamari.CommonTemp
{
    public abstract class Resolver<T>
    {
        readonly ILifetimeScope lifetimeScope;

        protected Resolver(ILifetimeScope lifetimeScope)
        {
            this.lifetimeScope = lifetimeScope;
        }

        public T Create<TBehaviour>() where TBehaviour : T
        {
            return lifetimeScope.Resolve<TBehaviour>();
        }
    }
}