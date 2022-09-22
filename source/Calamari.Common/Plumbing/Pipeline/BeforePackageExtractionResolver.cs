﻿using System;
 using Autofac;
 using Calamari.Common.Features.Behaviours;

 namespace Calamari.Common.Plumbing.Pipeline
{
    public class BeforePackageExtractionResolver: Resolver<IBeforePackageExtractionBehaviour>
    {
        public BeforePackageExtractionResolver(ILifetimeScope lifetimeScope) : base(lifetimeScope)
        {
        }
    }
}