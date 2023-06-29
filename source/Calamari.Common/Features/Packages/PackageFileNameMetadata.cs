using System;
using Octopus.Versioning;

namespace Calamari.Common.Features.Packages
{
    public class PackageFileNameMetadata
    {
        public PackageFileNameMetadata(string packageId, IVersion version, IVersion fileVersion, string extension)
        {
            PackageId = packageId;
            Version = version;
            FileVersion = fileVersion;
            Extension = extension;
        }

        public string PackageId { get; }
        public IVersion Version { get; }
        public IVersion FileVersion { get; }
        public string Extension { get; }
    }
}