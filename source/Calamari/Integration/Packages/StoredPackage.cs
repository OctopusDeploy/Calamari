using Octopus.Versioning.Metadata;

namespace Calamari.Integration.Packages
{
    public class StoredPackage
    {
        public PhysicalPackageMetadata Metadata { get; set; }
        public string FullPath { get; set; }

        public StoredPackage(PhysicalPackageMetadata metadata, string fullPath)
        {
            Metadata = metadata;
            FullPath = fullPath;
        }
    }
}
