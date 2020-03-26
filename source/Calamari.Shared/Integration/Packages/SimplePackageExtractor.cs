namespace Calamari.Integration.Packages
{
    public abstract class SimplePackageExtractor : IPackageExtractor
    {
        public abstract int Extract(string packageFile, string directory);
        public abstract string[] Extensions { get; }
    }
}