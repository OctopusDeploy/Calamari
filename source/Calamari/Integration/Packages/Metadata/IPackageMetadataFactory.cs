namespace Calamari.Integration.Packages.Metadata
{
    /// <summary>
    /// Defines a factory that is used to parse the metadata from a package id using 
    /// various IPackageIDParsers. 
    /// </summary>
    public interface IPackageMetadataFactory
    {
        BasePackageMetadata ParseMetadata(string packageId);
    }
}