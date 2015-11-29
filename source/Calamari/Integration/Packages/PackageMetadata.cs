namespace Calamari.Integration.Packages
{
    public class PackageMetadata
    {
        public string Id { get; set; }
        public string Version { get; set; }
        public string FileExtension { get; set; }
    }

    public class ExtendedPackageMetadata : PackageMetadata
    {
        public string Hash { get; set; }
    }
}