﻿using System;
 using Autofac;
 using Calamari.Common.Features.Behaviours;

 namespace Calamari.Common.Plumbing.Pipeline
{
    public class AfterPackageExtractionResolver: Resolver<IAfterPackageExtractionBehaviour>
    {
        public AfterPackageExtractionResolver(ILifetimeScope lifetimeScope) : base(lifetimeScope)
        {
        }
    }
}