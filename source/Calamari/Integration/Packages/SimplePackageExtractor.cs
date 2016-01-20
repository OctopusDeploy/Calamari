using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NuGet;

namespace Calamari.Integration.Packages
{
    public abstract class SimplePackageExtractor : IPackageExtractor
    {
        public PackageMetadata GetMetadata(string packageFile)
        {
            var pkg = new PackageMetadata();

            var metaDataSection = ExtractMatchingExtension(packageFile, pkg);

            if (string.IsNullOrEmpty(pkg.FileExtension))
            {
                throw new FileFormatException(string.Format("Unable to determine filetype of file \"{0}\"", packageFile));
            }

            ExtractIdAndVersion(metaDataSection, pkg);


            SemanticVersion version;
            if (string.IsNullOrEmpty(pkg.Version) || !SemanticVersion.TryParse(pkg.Version, out version))
            {
                throw new FileFormatException(string.Format("Unable to extract the package version from file \"{0}\"", packageFile));
            }

            if (string.IsNullOrEmpty(pkg.Id))
            {
                throw new FileFormatException(string.Format("Unable to extract the package Id from file \"{0}\"", packageFile));
            }

            return pkg;
        }

        private static void ExtractIdAndVersion(string metaDataSection, PackageMetadata pkg)
        {
            var nameParts = metaDataSection.Split('.');
            for (var i = 0; i < nameParts.Length; i++)
            {
                int num;
                if (int.TryParse(nameParts[i], out num))
                {
                    pkg.Id = string.Join(".", nameParts.Take(i));
                    pkg.Version = string.Join(".", nameParts.Skip(i));
                    break;
                }
            }
        }

        private string ExtractMatchingExtension(string packageFile, PackageMetadata pkg)
        {
            var fileName = Path.GetFileName(packageFile);
            var matchingExtension = Extensions.FirstOrDefault(fileName.EndsWith);
            var metaDataSection = string.Empty;
            if (matchingExtension != null)
            {
                metaDataSection = fileName.Substring(0, fileName.Length - matchingExtension.Length);
            }
            else
            {
                foreach (var ext in Extensions)
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
            
            pkg.FileExtension = matchingExtension;
            return metaDataSection;
        }


        public abstract int Extract(string packageFile, string directory, bool suppressNestedScriptWarning);
        public abstract string[] Extensions { get; }

        
    }
}