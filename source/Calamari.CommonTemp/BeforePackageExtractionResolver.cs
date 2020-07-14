using Autofac;

namespace Calamari.CommonTemp
{
    public class BeforePackageExtractionResolver: Resolver<IBeforePackageExtractionBehaviour>
    {
        public BeforePackageExtractionResolver(ILifetimeScope lifetimeScope) : base(lifetimeScope)
        {
        }
    }
}