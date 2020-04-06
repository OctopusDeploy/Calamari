
namespace Calamari.Integration.Packages
{
    public interface IPackageExtractor
    {
        int Extract(string packageFile, string directory);

        string[] Extensions { get; }
    }
}