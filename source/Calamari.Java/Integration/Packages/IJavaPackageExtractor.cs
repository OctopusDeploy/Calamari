using Calamari.Integration.Packages;

namespace Calamari.Java.Integration.Packages
{
    public interface IJavaPackageExtractor : IGenericPackageExtractor
    {
        void RePackArchive();
    }
}