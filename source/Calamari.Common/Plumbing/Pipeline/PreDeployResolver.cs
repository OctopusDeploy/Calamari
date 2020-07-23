﻿using System;
 using Autofac;

 namespace Calamari.Common.Plumbing.Pipeline
{
    public class PreDeployResolver: Resolver<IPreDeployBehaviour>
    {
        public PreDeployResolver(ILifetimeScope lifetimeScope) : base(lifetimeScope)
        {
        }
    }
}