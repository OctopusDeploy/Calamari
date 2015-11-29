namespace Calamari.Integration.Packages
{
    public interface IGenericPackageExtractor : IPackageExtractor
    {
        IPackageExtractor GetExtractor(string packageFile);
    }
}