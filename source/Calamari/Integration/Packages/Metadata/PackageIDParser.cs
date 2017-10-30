namespace Calamari.Integration.Packages.Metadata
{
    /// <summary>
    /// Defines a service for extracting metadata from package ids or package file names
    /// </summary>
    public interface IPackageIDParser
    {
        /// <summary>
        /// Extracts metadata from a package ID (i.e. no version information)
        /// </summary>
        /// <param name="packageID">The package id</param>
        /// <returns>The metadata assocaited with the package id</returns>
        BasePackageMetadata GetMetadataFromPackageID(string packageID);
        /// <summary>
        /// Extracts metadata from a package file name
        /// </summary>
        /// <param name="packageFile">The package file name</param>
        /// <param name="extensions">The extensions that this parser should know about</param>
        /// <returns>The metadata assocaited with the package file</returns>
        PackageMetadata GetMetadataFromPackageName(string packageFile, string[] extensions);
    }
}