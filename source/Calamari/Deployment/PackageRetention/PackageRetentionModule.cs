﻿using Autofac;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Deployment.PackageRetention.Caching;
using Calamari.Deployment.PackageRetention.Model;
using Calamari.Deployment.PackageRetention.Repositories;

namespace Calamari.Deployment.PackageRetention
{
    public class PackageRetentionModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<JsonJournalRepository>().As<IJournalRepository>();
            builder.RegisterType<VariableJsonJournalPathProvider>().As<IJsonJournalPathProvider>();
            builder.RegisterType<PackageJournal>().As<IManagePackageCache>().SingleInstance();
            builder.RegisterType<LeastFrequentlyUsedWithAgingSort>().As<ISortJournalEntries>();
            builder.RegisterType<PercentFreeDiskSpacePackageCacheCleaner>().As<IRetentionAlgorithm>();
            builder.RegisterType<PackageQuantityPackageCacheCleaner>().As<IRetentionAlgorithm>();
            base.Load(builder);
        }
    }
}