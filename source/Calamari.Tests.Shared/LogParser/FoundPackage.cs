using System;
using Octopus.Versioning;

namespace Calamari.Tests.Shared.LogParser
{
    public class FoundPackage
    {
        public FoundPackage(string packageId,
                            string version,
                            string? versionFormat,
                            string? remotePath,
                            string? hash,
                            string? fileExtension)
        {
            PackageId = packageId;

            if (!Enum.TryParse(versionFormat, out VersionFormat realVersionFormat))
                realVersionFormat = VersionFormat.Semver;
            ;

            Version = VersionFactory.CreateVersion(version, realVersionFormat);

            RemotePath = remotePath;
            Hash = hash;
            FileExtension = fileExtension;
        }

        public string PackageId { get; }
        public IVersion Version { get; }
        public string? RemotePath { get; }
        public string? Hash { get; }
        public string? FileExtension { get; }
    }
}