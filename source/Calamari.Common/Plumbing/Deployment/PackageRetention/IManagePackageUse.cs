using System;
using System.Collections.Generic;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.PackageRetention;
using Octopus.Versioning;

namespace Calamari.Common.Plumbing.Deployment.PackageRetention
{
    public interface IManagePackageUse
    {
        void RegisterPackageUse();
        void RegisterPackageUse(out bool packageRegistered);
        void RegisterPackageUse(PackageIdentity package, ServerTaskId serverTaskId);
        void RegisterPackageUse(PackageIdentity package, ServerTaskId serverTaskId, out bool packageRegistered);
        void DeregisterPackageUse(PackageIdentity package, ServerTaskId serverTaskId);
        void DeregisterPackageUse(PackageIdentity package, ServerTaskId serverTaskId, out bool packageRegistered);
        IEnumerable<IUsageDetails> GetUsage(PackageIdentity package);
        bool TryGetVersionFormat(PackageId packageId, string version, VersionFormat defaultFormat, out VersionFormat versionFormat);
        bool TryGetVersionFormat(PackageId packageId, ServerTaskId deploymentTaskID, VersionFormat defaultFormat, out VersionFormat format);
        bool HasLock(PackageIdentity package);
        void ExpireStaleLocks(TimeSpan timeBeforeExpiration);
        void ApplyRetention(long spaceNeeded);
    }
}