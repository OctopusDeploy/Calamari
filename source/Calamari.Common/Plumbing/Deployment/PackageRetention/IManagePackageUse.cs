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
        bool HasLock(PackageIdentity package);
        void ExpireStaleLocks(TimeSpan timeBeforeExpiration);
    }
}