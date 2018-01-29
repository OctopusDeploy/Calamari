using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Octopus.Versioning;
using Octopus.Versioning.Factories;
using Octopus.Versioning.Metadata;

namespace Calamari.Integration.Packages.NuGet
{
    internal class NuGetFileSystemDownloader
    {
        static readonly IVersionFactory VersionFactory = new VersionFactory();
        static readonly IMetadataFactory PackageMetadataFactory = new MetadataFactory();
        
        public static void DownloadPackage(string packageId, IVersion version, Uri feedUri, string targetFilePath)
        {
            if (!Directory.Exists(feedUri.LocalPath))
                throw new Exception($"Path does not exist: '{feedUri}'");

            // Lookup files which start with the name "<Id>." and attempt to match it with all possible version string combinations (e.g. 1.2.0, 1.2.0.0)
            // before opening the package. To avoid creating file name strings, we attempt to specifically match everything after the last path separator
            // which would be the file name and extension.
            var package = (from path in GetPackageLookupPaths(packageId, version, feedUri)
                    let p = new LocalNuGetPackage(path) 
                    where p.Metadata.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase) && 
                          VersionFactory.CreateVersion(p.Metadata.Version.ToString(), PackageMetadataFactory.GetMetadataFromPackageID(packageId).VersionFormat).Equals(version)
                    select p).FirstOrDefault();

            if (package == null)
                throw new Exception($"Could not find package {packageId} {version} in feed: '{feedUri}'");

            Log.VerboseFormat("Found package {0} version {1}", package.Metadata.Id, package.Metadata.Version);
            Log.Verbose("Downloading to: " + targetFilePath);

            using (var targetFile = File.Open(targetFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
               package.GetStream(stream =>
               {
                  stream.CopyTo(targetFile); 
               }); 
            }
        }

        static IEnumerable<string> GetPackageLookupPaths(string packageId, IVersion version, Uri feedUri)
        {
            // Files created by the path resolver. This would take into account the non-side-by-side scenario
            // and we do not need to match this for id and version.
            var packageFileName = GetPackageFileName(packageId, version);
            var filesMatchingFullName = GetPackageFiles(feedUri, packageFileName);

            if (version != null && version.Revision < 1)
            {
                // If the build or revision number is not set, we need to look for combinations of the format
                // * Foo.1.2.nupkg
                // * Foo.1.2.3.nupkg
                // * Foo.1.2.0.nupkg
                // * Foo.1.2.0.0.nupkg
                // To achieve this, we would look for files named 1.2*.nupkg if both build and revision are 0 and
                // 1.2.3*.nupkg if only the revision is set to 0.
                string partialName = version.Patch < 1 ?
                                        String.Join(".", packageId, version.Major, version.Minor) :
                                        String.Join(".", packageId, version.Major, version.Minor, version.Patch);
                partialName += "*" + ".nupkg";

                // Partial names would result is gathering package with matching major and minor but different build and revision.
                // Attempt to match the version in the path to the version we're interested in.
                var partialNameMatches = GetPackageFiles(feedUri, partialName).Where(path => FileNameMatchesPattern(packageId, version, path));
                return Enumerable.Concat(filesMatchingFullName, partialNameMatches);
            }
            return filesMatchingFullName;
        }

        static bool FileNameMatchesPattern(string packageId, IVersion version, string path)
        {
            var name = Path.GetFileNameWithoutExtension(path);

            // When matching by pattern, we will always have a version token. Packages without versions would be matched early on by the version-less path resolver
            // when doing an exact match.
            return name.Length > packageId.Length &&
                   VersionFactory.TryCreateVersion(
                       name.Substring(packageId.Length + 1), 
                       out IVersion parsedVersion, 
                       PackageMetadataFactory.GetMetadataFromPackageID(packageId).VersionFormat) &&
                   parsedVersion.Equals(version);
        }

        static IEnumerable<string> GetPackageFiles(Uri feedUri, string filter = null)
        {
            var feedPath = feedUri.LocalPath;

            filter = filter ?? "*" + ".nupkg";

            // Check for package files one level deep. We use this at package install time
            // to determine the set of installed packages. Installed packages are copied to
            // {id}.{version}\{packagefile}.{extension}.
            foreach (var dir in Directory.EnumerateDirectories(feedPath))
            {
                foreach (var path in GetFiles(dir, filter))
                {
                    yield return path;
                }
            }

            // Check top level directory
            foreach (var path in GetFiles(feedPath, filter))
            {
                yield return path;
            }
        }

        static IEnumerable<string> GetFiles(string path, string filter)
        {
            try
            {
                return Directory.EnumerateFiles(path, filter, SearchOption.TopDirectoryOnly);
            } 
            catch (UnauthorizedAccessException)
            { }
            catch (DirectoryNotFoundException)
            { }

            return Enumerable.Empty<string>();
        }

        static string GetPackageFileName(string packageId, IVersion version)
        {
            return packageId + "." + version + ".nupkg";
        }
    }
}