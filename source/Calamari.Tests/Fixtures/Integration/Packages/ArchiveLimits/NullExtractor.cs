using System;
using Calamari.Common.Features.Packages;
using SharpCompress.Archives;

namespace Calamari.Tests.Fixtures.Integration.Packages.ArchiveLimits
{
    public class NullExtractor : IPackageEntryExtractor
    {
        public string[] Extensions => new[] { ".zip" };
        public int Extract(string packageFile, string directory)
        {
            return 1;
        }

        public void ExtractEntry(string directory, IArchiveEntry entry)
        {
        }
    }
}