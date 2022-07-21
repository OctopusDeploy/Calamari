using System;
using Autofac;

namespace Calamari.Common.Plumbing.Pipeline
{
    public class OnFinishResolver: Resolver<IOnFinishBehaviour>
    {
        public OnFinishResolver(ILifetimeScope lifetimeScope) : base(lifetimeScope)
        {
        }
    }
}