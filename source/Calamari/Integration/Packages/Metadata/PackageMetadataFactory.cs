namespace Calamari.Integration.Packages.Metadata
{
    public class PackageMetadataFactory : IPackageMetadataFactory
    {
        public BasePackageMetadata ParseMetadata(string packageId)
        {
            try
            {
                /*
                 * Try the maven parsing first. Maven package IDs have been
                 * constructed so the NuGet packages will fail this parsing.
                 */
                return new MavenPackageIDParser().GetMetadataFromPackageID(packageId);
            }
            catch
            {
                /*
                 * NuGet is the default format, and if all other package identification
                 * fails, NuGet will be assumed.
                 */
                return new NuGetPackageIDParser().GetMetadataFromPackageID(packageId);    
            }            
        }
    }
}