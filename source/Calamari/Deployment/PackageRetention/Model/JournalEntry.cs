using System;

namespace Calamari.Deployment.PackageRetention.Model
{
    public class JournalEntry
    {
        public PackageID PackageID { get; }
        public PackageLocks PackageLocks { get; }
        public PackageUsage PackageUsage { get; }

        public JournalEntry(PackageID packageID, PackageLocks packageLocks = null, PackageUsage packageUsage = null)
        {
            PackageID = packageID ?? throw new ArgumentNullException(nameof(packageID));
            PackageLocks = packageLocks ?? new PackageLocks();
            PackageUsage = packageUsage ?? new PackageUsage();
        }
    }
}