using Octopus.Versioning.Metadata;

namespace Calamari.Integration.Packages
{
    public interface IPackageExtractor
    {
        PackageMetadata GetMetadata(string packageFile);
        int Extract(string packageFile, string directory, bool suppressNestedScriptWarning);

        string[] Extensions { get; }
    }
}