using System;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Deployment.PackageRetention.Model;

namespace Calamari.Tests.Fixtures.PackageRetention
{
    class UsageDetailsBuilder
    {
        CacheAge cacheAgeAtUsage;
        DateTime dateTime;
        ServerTaskId deploymentTaskId;

        public UsageDetails Build()
        {
            return new UsageDetails(deploymentTaskId, cacheAgeAtUsage, dateTime);
        }

        public UsageDetailsBuilder WithCacheAgeAtUsage(CacheAge cacheAge)
        {
            cacheAgeAtUsage = cacheAge;
            return this;
        }

        public UsageDetailsBuilder WithDateTime(DateTime usageDateTime)
        {
            dateTime = usageDateTime;
            return this;
        }

        public UsageDetailsBuilder WithDeploymentTaskId(ServerTaskId taskId)
        {
            deploymentTaskId = taskId;
            return this;
        }
    }
}