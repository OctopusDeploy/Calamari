using System;
using Octopus.Versioning;

namespace Calamari.Tests.Helpers
{
    // This file is a clone of how the server represents find package responses from Calamari
    public class FoundPackage
    {
        public string PackageId { get; }
        public IVersion Version { get; }
        public string RemotePath { get; }
        public string Hash { get; }
        public string FileExtension { get; }

        public FoundPackage(string packageId, string version, string versionFormat, string remotePath, string hash, string fileExtension)
        {
            PackageId = packageId;

            if (!Enum.TryParse(versionFormat, out VersionFormat realVersionFormat))
            {
                realVersionFormat = VersionFormat.Semver;
            };

            Version = VersionFactory.CreateVersion(version, realVersionFormat);
            
            RemotePath = remotePath;
            Hash = hash;
            FileExtension = fileExtension;
        }
    }
}