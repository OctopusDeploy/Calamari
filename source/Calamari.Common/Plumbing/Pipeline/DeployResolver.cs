﻿using System;
 using Autofac;

 namespace Calamari.Common.Plumbing.Pipeline
{
    public class DeployResolver: Resolver<IDeployBehaviour>
    {
        public DeployResolver(ILifetimeScope lifetimeScope) : base(lifetimeScope)
        {
        }
    }
}