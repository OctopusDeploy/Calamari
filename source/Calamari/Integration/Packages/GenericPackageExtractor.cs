using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Calamari.Integration.Packages
{
    public class GenericPackageExtractor : IGenericPackageExtractor
    {
        public string[] Extensions
        {
            get { return extractors.SelectMany(e => e.Extensions).OrderBy(e => e).ToArray(); }
        }

        public PackageMetadata GetMetadata(string packageFile)
        {
            return GetExtractor(packageFile).GetMetadata(packageFile);
        }

        public int Extract(string packageFile, string directory, bool suppressNestedScriptWarning)
        {
            return GetExtractor(packageFile).Extract(packageFile, directory, suppressNestedScriptWarning);
        }

        public IPackageExtractor GetExtractor(string packageFile)
        {
            if (string.IsNullOrEmpty(Path.GetExtension(packageFile)))
            {
                throw new FileFormatException("Package is missing file extension. This is needed to select the correct extraction algorithm.");
            }

            var extractor = ExtensionSuffix(packageFile);
            if (extractor != null)
                return extractor;


            extractor = ExtensionWithHashSuffix(packageFile);
            if (extractor != null)
                return extractor;

            throw new FileFormatException(string.Format("Unsupported file extension \"{0}\"", Path.GetExtension(packageFile)));
        }



        /// Order is important here since .tar.gz should be checked for before .gz
        private readonly List<IPackageExtractor> extractors = new List<IPackageExtractor>
        {
            new OpenPackagingConventionExtractor(),
            new TarGzipPackageExtractor(),
            new TarBzipPackageExtractor(),
            //new TarLzwPackageExtractor(), // For some reason this doesnt currently work...
            new ZipPackageExtractor(),
            new TarPackageExtractor()
        };

        private IPackageExtractor ExtensionWithHashSuffix(string packageFile)
        {
            return extractors.FirstOrDefault(p => p.Extensions.Any(ext => new Regex(Regex.Escape(ext) + "-[a-z0-9\\-]*$").IsMatch(packageFile)));
        }

        private IPackageExtractor ExtensionSuffix(string packageFile)
        {
            return extractors.FirstOrDefault(
                p => p.Extensions.Any(ext =>
                    packageFile.EndsWith(ext, StringComparison.InvariantCultureIgnoreCase)));
        }
    }
}