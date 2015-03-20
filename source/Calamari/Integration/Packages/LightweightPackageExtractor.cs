using System;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using NuGet;

namespace Calamari.Integration.Packages
{
    /// <summary>
    /// Given a 180mb NuGet package, NuGet.Core's PackageManager uses 1.17GB of memory and 55 seconds to extract it. 
    /// This is because it continually takes package files and copies them to byte arrays in memory to work with. 
    /// This class simply uses the packaging API's directly to extract, and only uses 6mb and takes 10 seconds on the 
    /// same 180mb file. 
    /// </summary>
    public class LightweightPackageExtractor : IPackageExtractor
    {
        static readonly string[] ExcludePaths = new[] { "_rels", "package\\services\\metadata" };

        public LightweightPackageExtractor()
        {
        }

        public PackageMetadata GetMetadata(string packageFile)
        {
            using (var package = Package.Open(packageFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return ProcessManifest(package);
            }
        }

        PackageMetadata ProcessManifest(Package package)
        {
            var packageRelationship =
                package.GetRelationshipsByType("http://schemas.microsoft.com/packaging/2010/07/manifest")
                .SingleOrDefault();

            if (packageRelationship == null)
            {
                throw new InvalidOperationException("Package does not contain a manifest");
            }

            var part = package.GetPart(packageRelationship.TargetUri);

            return ReadManifestStream(part.GetStream());
        }

        PackageMetadata ReadManifestStream(Stream manifestStream)
        {
            var result = new PackageMetadata();
            var manifest = Manifest.ReadFrom(manifestStream, validateSchema: false);
            var packageMetadata = (IPackageMetadata)manifest.Metadata;
            result.Id = packageMetadata.Id;
            result.Version = packageMetadata.Version.ToString();
            return result;
        }

        public void Install(string packageFile, string directory, bool suppressNestedScriptWarning, out int filesExtracted)
        {
            filesExtracted = 0;
            using (var package = Package.Open(packageFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var files =
                    from part in package.GetParts()
                    where IsPackageFile(part)
                    select part;

                foreach (var part in files)
                {
                    filesExtracted++;
                    var path = UriUtility.GetPath(part.Uri);

                    if (!suppressNestedScriptWarning)
                    {
                        WarnIfScriptInSubFolder(path);
                    }

                    path = Path.Combine(directory, path);

                    var parent = Path.GetDirectoryName(path);
                    if (parent != null && !Directory.Exists(parent))
                        Directory.CreateDirectory(parent);

                    using (var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                    using (var stream = part.GetStream())
                    {
                        stream.CopyTo(fileStream);
                        fileStream.Flush();
                    }
                }
            }
        }

        void WarnIfScriptInSubFolder(string path)
        {
            var fileName = Path.GetFileName(path);
            var directoryName = Path.GetDirectoryName(path);

            if (string.Equals(fileName, "Deploy.ps1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, "PreDeploy.ps1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, "PostDeploy.ps1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, "DeployFailed.ps1", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(directoryName))
                {
                    Log.WarnFormat("The script file \"{0}\" contained within the package will not be executed because it is contained within a child folder. As of Octopus Deploy 2.4, scripts in sub folders will not be executed.", path);
                }
            }
        }

        #region Code taken from nuget.codeplex.com, license: http://nuget.codeplex.com/license

        internal static bool IsPackageFile(PackagePart part)
        {
            var path = UriUtility.GetPath(part.Uri);
            return !ExcludePaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)) &&
                   !PackageUtility.IsManifest(path);
        }

        static class UriUtility
        {
            internal static string GetPath(Uri uri)
            {
                var path = uri.OriginalString;
                if (path.StartsWith("/", StringComparison.Ordinal))
                {
                    path = path.Substring(1);
                }
                return Uri.UnescapeDataString(path.Replace('/', Path.DirectorySeparatorChar));
            }
        }

        static class PackageUtility
        {
            public static bool IsManifest(string path)
            {
                var extension = Path.GetExtension(path);
                return extension != null && extension.Trim('.').Equals("nuspec", StringComparison.OrdinalIgnoreCase);
            }
        }

        #endregion
    }
}
