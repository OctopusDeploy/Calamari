using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Octopus.Core.Resources.Versioning;
using Octopus.Core.Resources.Versioning.Factories;

namespace Calamari.Integration.Packages.Metadata
{
    public class PackageIdentifier
    {
        static readonly IVersionFactory VersionFactory = new VersionFactory();
        
        /// <summary>
        /// Takes a string containing a concatenated package ID and version (e.g. a filename or database-key) and 
        /// attempts to parse a package ID and semantic version.  
        /// </summary>
        /// <param name="idAndVersion">The concatenated package ID and version.</param>
        /// <param name="packageId">The parsed package ID</param>
        /// <param name="version">The parsed semantic version</param>
        /// <returns>True if parsing was successful, else False</returns>
        public static bool TryParsePackageIdAndVersion(string idAndVersion, out string packageId, out IVersion version)
        {
            packageId = null;
            version = null;

            const string packageIdPattern = @"(?<packageId>(\w+([_.-]\w+)*?))";
            const string semanticVersionPattern = @"(?<semanticVersion>(\d+(\.\d+){0,3}" // Major Minor Patch
                 + @"(-[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?)" // Pre-release identifiers
                 + @"(\+[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?)"; // Build Metadata

            var match = Regex.Match(idAndVersion, $@"^{packageIdPattern}\.{semanticVersionPattern}$");
            var packageIdMatch = match.Groups["packageId"];
            var versionMatch = match.Groups["semanticVersion"];

            if (!packageIdMatch.Success || !versionMatch.Success)
                return false;

            packageId = packageIdMatch.Value;

            return VersionFactory.CanCreateSemanticVersion(versionMatch.Value, out version);
        }

        /// <summary>
        /// Given a package-file path and a list of valid extensions extracts the package-metadata component and the extension.  
        /// E.g: Given `C:\HelloWorld.1.0.0.zip-0d0010a3-d421-4e3d-9b28-49dad989281c` would return `{ "HelloWorld.1.0.0", ".zip" }` 
        /// (assuming `.zip` was in the list of valid extensions).
        /// </summary>
        /// <param name="packageFilePath">A package file path</param>
        /// <param name="validExtensions">A list of valid extensions</param>
        /// <returns>a Tuple where Item1 is the package metadata component and Item2 is the extension</returns>
        public static Tuple<string,string> ExtractPackageExtensionAndMetadata(string packageFilePath, ICollection<string> validExtensions)
        {
            var fileName = Path.GetFileName(packageFilePath);
            var matchingExtension = validExtensions.FirstOrDefault(fileName.EndsWith);
            var metaDataSection = string.Empty;
            if (matchingExtension != null)
            {
                metaDataSection = fileName.Substring(0, fileName.Length - matchingExtension.Length);
            }
            else
            {
                foreach (var ext in validExtensions)
                {
                    var match = new Regex("(?<extension>" + Regex.Escape(ext) + ")-[a-z0-9\\-]*$").Match(fileName);
                    if (match.Success)
                    {
                        matchingExtension = match.Groups["extension"].Value;
                        metaDataSection = fileName.Substring(0, match.Index);
                        break;
                    }
                }
            }

            return new Tuple<string, string>(metaDataSection, matchingExtension);
        }
    }
}