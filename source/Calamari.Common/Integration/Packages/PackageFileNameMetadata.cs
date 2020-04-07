using Octopus.Versioning;

namespace Calamari.Integration.Packages
{
    public class PackageFileNameMetadata
    {
        public string PackageId { get; }
        public IVersion Version { get; }
        public string Extension { get; }

        public PackageFileNameMetadata(string packageId, IVersion version, string extension)
        {
            PackageId = packageId;
            Version = version;
            Extension = extension;
        }
    }
}