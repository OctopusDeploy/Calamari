using Autofac;
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
            builder.RegisterType<LeastFrequentlyUsedWithAgingCacheAlgorithm>().As<IOrderJournalEntries>();
            builder.RegisterType<PercentFreeDiskSpacePackageCleaner>().As<IRetentionAlgorithm>();
            base.Load(builder);
        }
    }
}