﻿using System;
 using Autofac;

 namespace Calamari.Common.Plumbing.Pipeline
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