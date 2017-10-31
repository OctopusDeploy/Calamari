using Octopus.Core.Resources;
using Octopus.Core.Resources.Metadata;

namespace Calamari.Integration.Packages.Metadata
{
    /// <summary>
    /// Defines a factory that is used to parse the metadata from a package id, typically using 
    /// one more more implementations of IPackageIDParsers. 
    /// </summary>
    public interface IPackageMetadataFactory
    {
        PhysicalPackageMetadata ParseMetadata(string packageId, string version, long size, string hash);
        BasePackageMetadata ParseMetadata(string packageId);
    }
}