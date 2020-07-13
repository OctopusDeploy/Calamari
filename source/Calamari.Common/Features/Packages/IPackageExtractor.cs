
namespace Calamari.Common.Features.Packages
{
    public interface IPackageExtractor
    {
        int Extract(string packageFile, string directory);

        string[] Extensions { get; }
    }
}