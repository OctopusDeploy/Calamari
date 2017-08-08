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
        private readonly ICommandLineRunner commandLineRunner;
        private readonly ICalamariFileSystem fileSystem;

        public JarExtractor(ICommandLineRunner commandLineRunner, ICalamariFileSystem fileSystem)
        {
            this.commandLineRunner = commandLineRunner;
            this.fileSystem = fileSystem;
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

            var manifest = ExtractManifest(jarFile);

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

        string ExtractManifest(string jarFile)
        {
            const string manifestJarPath = "META-INF/MANIFEST.MF";

            var tempDirectory = fileSystem.CreateTemporaryDirectory();

            var extractJarCommand =
                new CommandLineInvocation("java", $"-cp tools.jar sun.tools.jar.Main xf \"{jarFile}\" \"{manifestJarPath}\"", tempDirectory);

            try
            {
                Log.Verbose($"Invoking '{extractJarCommand}' to extract '{manifestJarPath}'");
                var result = commandLineRunner.Execute(extractJarCommand);
                result.VerifySuccess();

                // Ensure our slashes point in the correct direction
                var extractedManifestPathComponents = new List<string>{tempDirectory}; 
                extractedManifestPathComponents.AddRange(manifestJarPath.Split('/'));

                return File.ReadAllText(Path.Combine(extractedManifestPathComponents.ToArray()));
            }
            catch (Exception ex)
            {
                throw new Exception($"Error invoking '{extractJarCommand}'", ex);
            }
            finally
            {
               fileSystem.DeleteDirectory(tempDirectory, FailureOptions.IgnoreFailure); 
            }
        }
    }
}