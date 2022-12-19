using System;
using SharpCompress.Archives;

namespace Calamari.Common.Features.Packages
{
    public interface IPackageExtractor
    {
        string[] Extensions { get; }
        int Extract(string packageFile, string directory);
    }

    public interface IPackageEntryExtractor : IPackageExtractor
    {
        void ExtractEntry(string directory, IArchiveEntry entry);
    }
}
