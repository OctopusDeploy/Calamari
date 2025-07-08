using System;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Deployment.PackageRetention.Model;

namespace Calamari.Tests.Fixtures.PackageRetention
{
    class JournalEntryBuilder
    {
        PackageIdentity package;
        PackageUsages usages;
        PackageLocks locks;
        ulong fileSizeBytes;
        
        public JournalEntry Build()
        {
            return new JournalEntry(package, fileSizeBytes, locks, usages);
        }

        public JournalEntryBuilder WithPackageIdentity(PackageIdentity packageIdentity)
        {
            package = packageIdentity;
            return this;
        }

        public JournalEntryBuilder WithPackageUsages(PackageUsages packageUsages)
        {
            usages = packageUsages;
            return this;
        }

        public JournalEntryBuilder WithPackageLocks(PackageLocks packageLocks)
        {
            locks = packageLocks;
            return this;
        }

        public JournalEntryBuilder WithFileSizeBytes(ulong bytes)
        {
            fileSizeBytes = bytes;
            return this;
        }
    }
}