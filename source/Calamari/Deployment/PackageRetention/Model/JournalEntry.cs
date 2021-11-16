using System;
using Calamari.Common.Plumbing.Deployment.PackageRetention;

namespace Calamari.Deployment.PackageRetention.Model
{
    public class JournalEntry
    {
        public PackageIdentity Package { get; }
        public PackageLocks PackageLocks { get; }
        public PackageUsage PackageUsage { get; }

        public JournalEntry(PackageIdentity package, PackageLocks packageLocks = null, PackageUsage packageUsage = null)
        {
            Package = package ?? throw new ArgumentNullException(nameof(package));
            PackageLocks = packageLocks ?? new PackageLocks();
            PackageUsage = packageUsage ?? new PackageUsage();
        }                                                                   
    }
}