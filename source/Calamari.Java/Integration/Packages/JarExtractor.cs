using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Calamari.Commands.Support;
using Calamari.Integration.Packages;
using Calamari.Integration.Processes;
using Calamari.Util;
using NuGet.Versioning;
using SharpCompress.Readers.Zip;

namespace Calamari.Java.Integration.Packages
{
    public class JarExtractor : IPackageExtractor
    {
        private readonly ICommandLineRunner commandLineRunner;

        public JarExtractor(ICommandLineRunner commandLineRunner)
        {
            this.commandLineRunner = commandLineRunner;
        }

        public string[] Extensions => new[] {".jar", ".war"}; 

        public int Extract(string packageFile, string directory, bool suppressNestedScriptWarning)
        {
            var extractJarCommand =
                new CommandLineInvocation("java", $"-cp tools.jar sun.tools.jar.Main xf \"{packageFile}\"", directory);

            Log.Verbose($"Invoking '{extractJarCommand}' to extract '{packageFile}'");
            var result = commandLineRunner.Execute(extractJarCommand);
            result.VerifySuccess();

            var count = -1;

            try
            {
                count = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Count(); 
            }
            catch (Exception ex)
            {
                Log.Verbose(
                    $"Unable to return extracted file count. Error while enumerating '{directory}':\n{ex.Message}");
            }

            return count;
        }

        public PackageMetadata GetMetadata(string packageFile)
        {
            var metadataAndExtension = PackageIdentifier.ExtractPackageExtensionAndMetadata(packageFile, Extensions);

            var idAndVersion = metadataAndExtension.Item1;
            var pkg = new PackageMetadata {FileExtension = metadataAndExtension.Item2};

            if (string.IsNullOrEmpty(pkg.FileExtension))
            {
                throw new CommandException(string.Format("Unable to determine filetype of file \"{0}\"", packageFile));
            }

            using (var fileStream = new FileStream(packageFile, FileMode.Open)) 
            {
                DeterminePackageIdAndVersion(idAndVersion, fileStream, out string packageId, out NuGetVersion packageVersion);

                pkg.Id = packageId;
                pkg.Version = packageVersion.ToString();
                return pkg;
            }
        }

        static void DeterminePackageIdAndVersion(string fileNameWithoutExtension, Stream fileStream, out string packageId, out NuGetVersion packageVersion)
        {
            var manifest = ReadManifest(fileStream);
            var versionFromManifest = GetVersionFromManifest(manifest);

            if (versionFromManifest != null)
            {
                packageVersion = versionFromManifest;

                if (!PackageIdentifier.TryParsePackageIdAndVersion(fileNameWithoutExtension, out packageId, out NuGetVersion ignoredVersionFromFileName))
                {
                    throw new CommandException($"Could not parse package ID from '{fileNameWithoutExtension}'");
                }
            }
            else
            {
                if (!PackageIdentifier.TryParsePackageIdAndVersion(fileNameWithoutExtension, out packageId, out packageVersion))
                {
                    throw new CommandException($"Could not parse package ID and version from '{fileNameWithoutExtension}'");
                }
            }
        }

        static NuGetVersion GetVersionFromManifest(IDictionary<string, string> manifest)
        {
            if (!manifest.TryGetValue("Implementation-Version", out string versionFromManifest))
                return null;

            return !NuGetVersion.TryParse(versionFromManifest, out NuGetVersion semanticVersion)
                ? null
                : semanticVersion;
        }

        static bool IsManifest(string path)
        {
            return path.Equals("META-INF/MANIFEST.MF", StringComparison.OrdinalIgnoreCase);
        }

        static IDictionary<string, string> ReadManifest(Stream jarFile)
        {
            var map = new Dictionary<string, string>();

            using (var reader = ZipReader.Open(jarFile))
            {
                while (reader.MoveToNextEntry())
                {
                    if (reader.Entry.IsDirectory || !IsManifest(reader.Entry.Key))
                        continue;

                    using (var manifestStream = reader.OpenEntryStream())
                    using (var manifest = new StreamReader(manifestStream))
                    {
                        while (!manifest.EndOfStream)
                        {
                            var line = manifest.ReadLine();

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

                return map;
            }
        }
    }
}