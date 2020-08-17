using System;
using Octopus.Versioning;

namespace Calamari.Common.Features.Packages
{
    public class PackageFileNameMetadata
    {
        public PackageFileNameMetadata(string packageId, IVersion version, string extension)
        {
            PackageId = packageId;
            Version = version;
            Extension = extension;
        }

        public string PackageId { get; }
        public IVersion Version { get; }
        public string Extension { get; }
    }
}