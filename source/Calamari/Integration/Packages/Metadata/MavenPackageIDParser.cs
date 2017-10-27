using System.IO;
using Calamari.Constants;
using Octopus.Core.Resources;

namespace Calamari.Integration.Packages.Metadata
{
    /// <summary>
    /// Maven package IDs come in the format: group#artifact
    /// </summary>
    public class MavenPackageIDParser : IPackageIDParser
    {
        public BasePackageMetadata GetyMetadata(string packageID)
        {
            var idAndVersionSplit = packageID.Split(JavaConstants.JAVA_FILENAME_DELIMITER);

            if (idAndVersionSplit.Length != 2)
            {
                throw new FileFormatException(
                    $"Unable to extract the package ID from \"{packageID}\"");
            }

            return new BasePackageMetadata()
            {
                Id = packageID,
                FeedType = FeedType.Maven
            };
        }
    }
}