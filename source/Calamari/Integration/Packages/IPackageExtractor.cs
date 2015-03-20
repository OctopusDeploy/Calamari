namespace Calamari.Integration.Packages
{
    public interface IPackageExtractor
    {
        PackageMetadata GetMetadata(string packageFile);
        void Install(string packageFile, string directory, bool suppressNestedScriptWarning, out int filesExtracted);
    }
}