using System;
using System.IO;
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
            Log.Error("JarExtractor Extract");

            return jarTool.ExtractJar(packageFile, directory);
        }

        public override PackageMetadata GetMetadata(string packageFile)
        {
            var metadataAndExtension =
                PackageIdentifier.ExtractPackageExtensionAndMetadata(packageFile, Extensions);

            var idAndVersion = metadataAndExtension.Item1;
            var pkg = new PackageMetadata {FileExtension = metadataAndExtension.Item2};

            if (string.IsNullOrEmpty(pkg.FileExtension))
            {
                throw new FileFormatException($"Unable to determine filetype of file \"{packageFile}\"");
            }

            var idAndVersionSplit = idAndVersion.Split('#');

            if (idAndVersionSplit.Length != 3)
            {
                throw new FileFormatException(
                    $"Unable to extract the package ID and version from file \"{packageFile}\"");
            }

            pkg.Id = idAndVersionSplit[0] + "#" + idAndVersionSplit[1];
            pkg.Version = idAndVersionSplit[2];

            return pkg;
        }
    }
}