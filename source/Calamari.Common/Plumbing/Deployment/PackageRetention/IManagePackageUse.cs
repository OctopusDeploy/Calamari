using System;
using System.Collections.Generic;
using Calamari.Common.Plumbing.Variables;
using Octopus.Versioning;

namespace Calamari.Common.Plumbing.Deployment.PackageRetention
{
    public interface IManagePackageUse 
    {
        void RegisterPackageUse(IVariables variables);
        void RegisterPackageUse(PackageIdentity package, ServerTaskId serverTaskId);
        void DeregisterPackageUse(PackageIdentity package, ServerTaskId serverTaskId);
        IEnumerable<DateTime> GetUsage(PackageIdentity package);
        bool HasLock(PackageIdentity package);
        void ApplyRetention(ulong spaceRequired);
        void ExpireStaleLocks();
    }
}