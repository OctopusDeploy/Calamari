using Octopus.Core.Resources;

namespace Calamari.Integration.Packages.Metadata
{
    /// <summary>
    /// Defines a factory that is used to parse the metadata from a package id, typically using 
    /// one more more implementations of IPackageIDParsers. 
    /// </summary>
    public interface IPackageMetadataFactory
    {
        BasePackageMetadata ParseMetadata(string packageId);
    }
}