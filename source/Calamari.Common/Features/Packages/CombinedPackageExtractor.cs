using System;
using System.IO;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages.Decorators;
using Calamari.Common.Features.Packages.Java;
using Calamari.Common.Features.Packages.NuGet;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Features.Packages
{
    public interface ICombinedPackageExtractor : IPackageExtractor
    {
    }

    public class CombinedPackageExtractor : ICombinedPackageExtractor
    {
        readonly IPackageExtractor[] extractors;

        public CombinedPackageExtractor(ILog log, IVariables variables, ICommandLineRunner commandLineRunner)
        {
            extractors = new IPackageExtractor[]
            {
                // Order is important here since .tar.gz should be checked for before .gz
                new NupkgExtractor(log),
                new TarGzipPackageExtractor(log),
                new TarBzipPackageExtractor(log),
                new ZipPackageExtractor(log),
                new TarPackageExtractor(log),
                new JarPackageExtractor(new JarTool(commandLineRunner, log, variables))
            }.Select(e => e.WithExtractionLimits(log, variables)).ToArray();
        }

        public string[] Extensions => extractors.SelectMany(e => e.Extensions).OrderBy(e => e).ToArray();

        public int Extract(string packageFile, string directory)
        {
            return GetExtractor(packageFile).Extract(packageFile, directory);
        }

        public IPackageExtractor GetExtractor(string packageFile)
        {
            var extension = Path.GetExtension(packageFile);
            if (string.IsNullOrEmpty(extension))
                throw new CommandException("Package is missing file extension. This is needed to select the correct extraction algorithm.");

            var file = PackageName.FromFile(packageFile);
            var extractor = extractors.FirstOrDefault(p => p.Extensions.Any(ext => file.Extension.Equals(ext, StringComparison.OrdinalIgnoreCase)));
            if (extractor == null)
                throw new CommandException($"Unsupported file extension `{extension}`");

            return extractor;
        }
    }
}
