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
        void RegisterPackageUse(PackageIdentity package, ServerTaskId serverTaskId);
        void DeregisterPackageUse(PackageIdentity package, ServerTaskId serverTaskId);
        IEnumerable<IUsageDetails> GetUsage(PackageIdentity package);
        bool TryGetVersionFormat(PackageId packageId, string version, out VersionFormat versionFormat);
        bool HasLock(PackageIdentity package);
        void ExpireStaleLocks();
    }
}