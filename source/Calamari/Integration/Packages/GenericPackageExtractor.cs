using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Calamari.Integration.Packages.NuGet;
using Calamari.Support;
using Octopus.Versioning;
using Octopus.Versioning.Metadata;

namespace Calamari.Integration.Packages
{
    public class GenericPackageExtractor : IGenericPackageExtractor
    {
        private const string UUIDSuffix = "-[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$";
        private const string ExtensionRegex = "(.*?)" + UUIDSuffix;

        private readonly List<IPackageExtractor> additionalExtractors =
            new List<IPackageExtractor>();

        private readonly ISupportLinkGenerator supportLinkGenerator = new SupportLinkGenerator();

        public GenericPackageExtractor()
        {
        }

        /// <summary>
        /// Construct a generic extractor supplying a list of additional extractors
        /// that should be considered after the generic list has been exhausted.
        /// </summary>
        /// <param name="additionalExtractors">A list of additional extractors that are to be considered when dealing with packages</param>
        public GenericPackageExtractor(List<IPackageExtractor> additionalExtractors)
        {
            this.additionalExtractors.AddRange(additionalExtractors);
        }

        public string[] Extensions
        {
            get { return Extractors.SelectMany(e => e.Extensions).OrderBy(e => e).ToArray(); }
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
                throw new FileFormatException(
                    "Package is missing file extension. This is needed to select the correct extraction algorithm.");
            }

            var combinedList = ExtensionSuffix(packageFile)
                .Union(ExtensionWithHashSuffix(packageFile))
                .ToList();

            /*
             * Start by finding an extractor that can successfully parse the metadata of the given file.
             * This will work in practice, although fails for some tests where a mock package is
             * supplied that may not actually be able to be extracted because the mock files don't contain
             * metadata.
             */
            return combinedList
                       .Select(extractor =>
                       {
                           try
                           {
                               /*
                                * Different extractors can share extensions. Attempt to get the metadata from the package
                                * file, and if no exception is thrown, then we have a valid extractor. 
                                */
                               return new Tuple<IPackageExtractor, PackageMetadata>(extractor,
                                   extractor.GetMetadata(packageFile));
                           }
                           catch
                           {
                               return null;
                           }
                       })
                       .Where(details => details != null)
                       .OrderByDescending(details => details.Item2.VersionFormat.Precedence())
                       .Select(details => details.Item1)
                       .FirstOrDefault() ?? 
                   /*
                    * If none of the extractors successfully parsed the metadata, fallback to the
                    * first one that matches the extension.
                    */
                   combinedList.FirstOrDefault() ??       
                   /*
                    * If all else fails then throw an exception.
                    */
                   ReportInvalidExtension(packageFile);
        }

        IPackageExtractor ReportInvalidExtension(string packageFile)
        {
            var extensionMatch = Regex.Match(Path.GetExtension(packageFile), ExtensionRegex);

            throw new FileFormatException(supportLinkGenerator.GenerateSupportMessage(
                string.Format(
                    "This step supports packages with the following extenions: {0}.\n" +
                    "The supplied package has the extension \"{1}\" which is not supported.",
                    Extractors.SelectMany(e => e.Extensions)
                        .Distinct()
                        .Aggregate((result, e) => result + ", " + e),
                    extensionMatch.Success ? extensionMatch.Groups[1].Value : Path.GetExtension(packageFile)),
                "JAVA-DEPLOY-ERROR-0001"));
        }

        internal static void WarnIfScriptInSubFolder(string path)
        {
            var fileName = Path.GetFileName(path);

            if (string.Equals(fileName, "Deploy.ps1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, "PreDeploy.ps1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, "PostDeploy.ps1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, "DeployFailed.ps1", StringComparison.OrdinalIgnoreCase))
            {
                var directoryName = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directoryName))
                {
                    Log.WarnFormat(
                        "The script file \"{0}\" contained within the package will not be executed because it is contained within a child folder. As of Octopus Deploy 2.4, scripts in sub folders will not be executed.",
                        path);
                }
            }
        }

        /// Order is important here since .tar.gz should be checked for before .gz
        protected virtual IList<IPackageExtractor> Extractors => new List<IPackageExtractor>
        {
            new NupkgExtractor(),
            new TarGzipPackageExtractor(),
            new TarBzipPackageExtractor(),
            //new TarLzwPackageExtractor(), // For some reason this doesnt currently work...
            new ZipPackageExtractor(),
            new TarPackageExtractor()
        }.Concat(additionalExtractors).ToList();

        private IEnumerable<IPackageExtractor> ExtensionWithHashSuffix(string packageFile)
        {
            return Extractors.Where(p =>
                p.Extensions.Any(ext => new Regex(Regex.Escape(ext) + UUIDSuffix).IsMatch(packageFile)));
        }

        private IEnumerable<IPackageExtractor> ExtensionSuffix(string packageFile)
        {
            return Extractors.Where(
                p => p.Extensions.Any(ext =>
                    packageFile.EndsWith(ext, StringComparison.OrdinalIgnoreCase)));
        }
    }
}