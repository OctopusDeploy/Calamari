namespace Calamari.Tests.Helpers
{
    // This file is a clone of how the server represents find package responses from Calamari
    public class FoundPackage
    {
        public string PackageId { get; }
        public string Version { get; }
        public string RemotePath { get; }
        public string Hash { get; }
        public string FileExtension { get; }

        public FoundPackage(string packageId, string version, string remotePath, string hash, string fileExtension)
        {
            PackageId = packageId;
            Version = version;
            RemotePath = remotePath;
            Hash = hash;
            FileExtension = fileExtension;
        }
    }
}