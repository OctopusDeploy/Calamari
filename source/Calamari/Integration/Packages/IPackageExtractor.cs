
namespace Calamari.Integration.Packages
{
    public interface IPackageExtractor
    {
        int Extract(string packageFile, string directory, bool suppressNestedScriptWarning);

        string[] Extensions { get; }
    }
}