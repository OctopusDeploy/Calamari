using System;

namespace Calamari.Common.Plumbing.Deployment.PackageRetention
{
    public interface IManagePackageCache
    {
        void RegisterPackageUse(PackageIdentity package, ServerTaskId deploymentTaskId, ulong packageSizeBytes);
        void RemoveAllLocks(ServerTaskId serverTaskId);
        void ApplyRetention();
        void ExpireStaleLocks(TimeSpan timeBeforeExpiration);
    }
}