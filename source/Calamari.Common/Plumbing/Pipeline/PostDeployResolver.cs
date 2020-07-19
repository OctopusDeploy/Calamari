﻿using System;
 using Autofac;

 namespace Calamari.Common.Plumbing.Pipeline
{
    public class PostDeployResolver: Resolver<IPostDeployBehaviour>
    {
        public PostDeployResolver(ILifetimeScope lifetimeScope) : base(lifetimeScope)
        {
        }
    }
}