using Octopus.Core.Resources;

namespace Calamari.Integration.Packages.Metadata
{
    /// <summary>
    /// NuGet is considered the fallback that will always match the supplied package id
    /// </summary>
    public class NuGetPackageIDParser : IPackageIDParser
    {
        public BasePackageMetadata GetyMetadata(string packageID)
        {
            return new BasePackageMetadata()
            {
                Id = packageID,
                FeedType = FeedType.NuGet
            };
        }
    }
}