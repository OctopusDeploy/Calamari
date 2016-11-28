namespace Calamari.Extensibility.Features
{
    public interface IPackageExtractor
    {
        string Extract(string package, PackageExtractionDestination extractionDestination);
    }
}