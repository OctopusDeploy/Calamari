using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Integration.Packages.NuGet;
using Calamari.Support;

namespace Calamari.Integration.Packages
{
    public class GenericPackageExtractor : IGenericPackageExtractor
    {
        readonly ILog log;

        private readonly List<IPackageExtractor> additionalExtractors =
            new List<IPackageExtractor>();

        private readonly ISupportLinkGenerator supportLinkGenerator = new SupportLinkGenerator();

        public GenericPackageExtractor(ILog log)
        {
            this.log = log;
        }

        /// <summary>
        /// Construct a generic extractor supplying a list of additional extractors
        /// that should be considered after the generic list has been exhausted.
        /// </summary>
        /// <param name="additionalExtractors">A list of additional extractors that are to be considered when dealing with packages</param>
        public GenericPackageExtractor(ILog log, List<IPackageExtractor> additionalExtractors)
        {
            this.log = log;
            this.additionalExtractors.AddRange(additionalExtractors);
        }

        public string[] Extensions
        {
            get { return Extractors.SelectMany(e => e.Extensions).OrderBy(e => e).ToArray(); }
        }

        public int Extract(string packageFile, string directory)
        {
            return GetExtractor(packageFile).Extract(packageFile, directory);
        }

        public IPackageExtractor GetExtractor(string packageFile)
        {
            var extension = Path.GetExtension(packageFile);
            if (string.IsNullOrEmpty(extension))
            {
                throw new FileFormatException("Package is missing file extension. This is needed to select the correct extraction algorithm.");
            }

            var file = PackageName.FromFile(packageFile);
            if (!Extensions.Contains(file.Extension))
            {
                throw new FileFormatException($"Unsupported file extension `{extension}`");
            }

            var extractor = FindByExtension(file);
            if (extractor != null)
                return extractor;
            
            throw new FileFormatException(supportLinkGenerator.GenerateSupportMessage(
                $"This step supports packages with the following extensions: {Extractors.SelectMany(e => e.Extensions).Distinct().Aggregate((result, e) => result + ", " + e)}.\n" +
                $"The supplied package has the extension \"{file.Extension}\" which is not supported.",
                "JAVA-DEPLOY-ERROR-0001"));
        }
        
        /// Order is important here since .tar.gz should be checked for before .gz
        protected virtual IList<IPackageExtractor> Extractors => new List<IPackageExtractor>
        {
            new NupkgExtractor(log),
            new TarGzipPackageExtractor(log),
            new TarBzipPackageExtractor(log),
            new ZipPackageExtractor(log),
            new TarPackageExtractor(log)
        }.Concat(additionalExtractors).ToList();

        private IPackageExtractor FindByExtension(PackageFileNameMetadata packageFile)
        {
            return Extractors.FirstOrDefault(p => p.Extensions.Any(ext => packageFile.Extension.Equals(ext, StringComparison.OrdinalIgnoreCase)));
        }
    }
}