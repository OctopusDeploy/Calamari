using System;
using Octopus.Versioning;

namespace Calamari.Common.Plumbing.Deployment.PackageRetention
{
    public interface IManagePackageUse
    {
        void RegisterPackageUse(PackageIdentity package, ServerTaskId serverTaskId);
        void DeregisterPackageUse(PackageIdentity package, ServerTaskId serverTaskId);
        bool TryGetVersionFormat(PackageId packageId, string version, VersionFormat defaultFormat, out VersionFormat versionFormat);
        bool TryGetVersionFormat(PackageId packageId, ServerTaskId deploymentTaskID, VersionFormat defaultFormat, out VersionFormat format);
        void ApplyRetention(string packageDirectory);
        void ExpireStaleLocks(TimeSpan timeBeforeExpiration);
    }
}