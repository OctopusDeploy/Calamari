using System;
using System.Collections.Generic;
using Calamari.Common.Plumbing.Variables;
using Octopus.Versioning;

namespace Calamari.Common.Plumbing.Deployment.PackageRetention
{
    public interface IJournal
    {
        void RegisterPackageUse(string packageID, IVersion version, string deploymentID);
        void RegisterPackageUse(IVariables variables);
        void RegisterPackageUse(PackageIdentity package, ServerTaskID serverTaskID);
        void DeregisterPackageUse(PackageIdentity package, ServerTaskID serverTaskID);
        bool HasLock(PackageIdentity package);
        IEnumerable<DateTime> GetUsage(PackageIdentity package);
        void ExpireStaleLocks();
    }
}