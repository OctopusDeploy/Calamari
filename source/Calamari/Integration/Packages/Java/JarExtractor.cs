using System;
using System.IO;
using Calamari.Constants;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;

namespace Calamari.Integration.Packages.Java
{
    public class JarExtractor : SimplePackageExtractor
    {
        readonly JarTool jarTool;

        public JarExtractor(ICommandLineRunner commandLineRunner, ICommandOutput commandOutput,
            ICalamariFileSystem fileSystem)
        {
            jarTool = new JarTool(commandLineRunner, commandOutput, fileSystem);
        }

        public override string[] Extensions => new[] {".jar", ".war", ".ear", ".rar"};

        public override int Extract(string packageFile, string directory, bool suppressNestedScriptWarning)
        {
            return jarTool.ExtractJar(packageFile, directory);
        }

        /// <summary>
        /// Java packages are in the unqiue situation where they can be sourced from the built-in library
        /// (which follows NuGet packaging and versioning formats) or from a Maven feed. We have no way 
        /// of knowing which feed the package was sourced from originally, so we we have to assume
        /// it could be either one.
        /// 
        /// The NuGet and Maven filenames are incompatible. This is by design; see 
        /// Octopus.Core.Packages.Maven.MavenPackageID.DELIMITER. NuGet parsing routines will not succeed 
        /// for a Maven package, and Maven parsing will not succeed for a NuGet package.
        /// 
        /// We take advantage of this to fallback through the various parsing routines until one works.
        /// </summary>
        /// <param name="packageFile">Filename to process</param>
        /// <returns>The package metadata</returns>
        public override PackageMetadata GetMetadata(string packageFile)
        {
            try
            {
                return base.GetMetadata(packageFile);
            }
            catch
            {
                return GetMavenMetadata(packageFile);
            }           
        }

        PackageMetadata GetMavenMetadata(string packageFile)
        {
            var metadataAndExtension =
                PackageIdentifier.ExtractPackageExtensionAndMetadata(packageFile, Extensions);

            var idAndVersion = metadataAndExtension.Item1;
            var pkg = new PackageMetadata {FileExtension = metadataAndExtension.Item2};

            if (string.IsNullOrEmpty(pkg.FileExtension))
            {
                throw new FileFormatException($"Unable to determine filetype of file \"{packageFile}\"");
            }

            var idAndVersionSplit = idAndVersion.Split(JavaConstants.JAVA_FILENAME_DELIMITER);

            if (idAndVersionSplit.Length != 3)
            {
                throw new FileFormatException(
                    $"Unable to extract the package ID and version from file \"{packageFile}\"");
            }

            pkg.Id = idAndVersionSplit[0] + JavaConstants.JAVA_FILENAME_DELIMITER + idAndVersionSplit[1];
            pkg.Version = idAndVersionSplit[2];

            return pkg;
        }
    }
}