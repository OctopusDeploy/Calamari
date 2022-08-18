using System;
using Autofac;

namespace Calamari.Common.Plumbing.Pipeline
{
    public class PackageExtractionResolver: Resolver<IPackageExtractionBehaviour>
    {
        public PackageExtractionResolver(ILifetimeScope lifetimeScope) : base(lifetimeScope)
        {
        }
    }
}