using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Calamari.Commands.Support;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;
using Calamari.Integration.Processes;
using NuGet.Versioning;

namespace Calamari.Java.Integration.Packages
{
    public class JarExtractor : IPackageExtractor
    {
        readonly ICommandLineRunner commandLineRunner;
        readonly ICalamariFileSystem fileSystem;
        readonly JarTool jarTool;


        public JarExtractor(ICommandLineRunner commandLineRunner, ICalamariFileSystem fileSystem)
        {
            this.commandLineRunner = commandLineRunner;
            this.fileSystem = fileSystem;
            this.jarTool = new JarTool(commandLineRunner, fileSystem);
        }

        public string[] Extensions => new[] {".jar", ".war"}; 

        public int Extract(string packageFile, string directory, bool suppressNestedScriptWarning)
        {
            return jarTool.ExtractJar(packageFile, directory);
        }

        public PackageMetadata GetMetadata(string packageFile)
        {
            var metadataAndExtension = PackageIdentifier.ExtractPackageExtensionAndMetadata(packageFile, Extensions);

            var idAndVersion = metadataAndExtension.Item1;

            if (string.IsNullOrEmpty(metadataAndExtension.Item2))
            {
                throw new CommandException(string.Format("Unable to determine filetype of file \"{0}\"", packageFile));
            }

            var manifest = ReadManifest(packageFile);

            string packageId;
            var packageVersion = GetVersionFromManifest(manifest);;

            if (packageVersion != null)
            {
                if (!PackageIdentifier.TryParsePackageIdAndVersion(idAndVersion, out packageId, out NuGetVersion ignoredVersionFromFileName))
                {
                    throw new CommandException($"Could not parse package ID from '{idAndVersion}'");
                }
            }
            else
            {
                if (!PackageIdentifier.TryParsePackageIdAndVersion(idAndVersion, out packageId, out packageVersion))
                {
                    throw new CommandException($"Could not parse package ID and version from '{idAndVersion}'");
                }
            }

            return new PackageMetadata
            {
                FileExtension = metadataAndExtension.Item2,
                Id = packageId,
                Version = packageVersion.ToString()
            };
        }

        static NuGetVersion GetVersionFromManifest(IDictionary<string, string> manifest)
        {
            if (!manifest.TryGetValue("Implementation-Version", out string versionFromManifest))
                return null;

            return !NuGetVersion.TryParse(versionFromManifest, out NuGetVersion semanticVersion)
                ? null
                : semanticVersion;
        }

        IDictionary<string, string> ReadManifest(string jarFile)
        {
            var map = new Dictionary<string, string>();

            var manifest = jarTool.ExtractManifest(jarFile);

            foreach (var line in manifest.Split(new[] {"\r\n", "\n"}, StringSplitOptions.RemoveEmptyEntries))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var match = Regex.Match(line, @"^(?<key>(.*)):\s?(?<value>.*)");

                var keyMatch = match.Groups["key"];
                var valueMatch = match.Groups["value"];

                if (!keyMatch.Success || !valueMatch.Success)
                {
                    throw new Exception($"Could not parse manifest line: '{line}'");
                }

                map.Add(keyMatch.Value, valueMatch.Value);
            }

            return map;
        }
    }
}