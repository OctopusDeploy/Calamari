using System;
using Octopus.Core.Resources.Versioning;

namespace Calamari.Integration.Packages
{
    public enum FeedType
    {
        None = 0,
        NuGet,
        Docker,
        Maven
    }

    public static class FeedTypeExtensions
    {
        /// <summary>
        /// Returns the precedence of a feed type. Feeds with higher precedence will be 
        /// prefered when multiple feeds can support the same extension type.
        /// 
        /// Typically the only precedence that matters is between NuGet (which is also considered
        /// to be the built in library) and a more specific feed like Maven. For example, both
        /// these feeds support .zip, .jar, .war, .ear etc files. But because the Maven feed has
        /// a higher priority, if the package can be parsed as a maven feed package, it will be
        /// preferred over any parsing that shows that it could be a nuget package.
        /// </summary>
        /// <param name="self">The FeedType enum</param>
        /// <returns>The precidence</returns>
        /// <exception cref="Exception"></exception>
        public static int Precedence(this VersionFormat self)
        {
            switch (self)
            {
                case VersionFormat.Semver:
                    return 0;
                case VersionFormat.Maven:
                    return 1;
                default:
                    throw new Exception("Invalid Version Format");
            }
        }
    }
}
