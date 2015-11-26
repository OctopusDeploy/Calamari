using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Calamari.Integration.Packages
{
    public class PackageExtractorFactory
    {

        /// Order is important here since .tar.gz should be checked for before .gz
        private readonly List<IPackageExtractor> extractors = new List<IPackageExtractor>()
        {
            new OpenPackagingConventionExtractor(),
            new TarGzipPackageExtractor(),
            new TarBzipPackageExtractor(),
            //new TarLzwPackageExtractor(), // For some reason this doesnt currently work...
            new ZipPackageExtractor(),
            new TarPackageExtractor()
        };

        public string[] ValidExtensions
        {
            get { return extractors.SelectMany(e => e.Extensions).OrderBy(e => e).ToArray(); }
        }

        public IPackageExtractor GetExtractor(string packageFile)
        {
            if (string.IsNullOrEmpty(Path.GetExtension(packageFile)))
            {
                throw new FileFormatException("Package is missing file extension. This is needed to select the correct extraction algorithm.");
            }

            var extractor = extractors.FirstOrDefault(p => p.Extensions.Any(name => packageFile.EndsWith(name, StringComparison.InvariantCultureIgnoreCase)));

            if (extractor == null)
            {
                throw new FileFormatException(string.Format("Unsupported file extension \"{0}\"", Path.GetExtension(packageFile)));
            }

            return extractor;
        }
    }
}
