using Autofac;

namespace Calamari.CommonTemp
{
    public class AfterPackageExtractionResolver: Resolver<IAfterPackageExtractionBehaviour>
    {
        public AfterPackageExtractionResolver(ILifetimeScope lifetimeScope) : base(lifetimeScope)
        {
        }
    }
}