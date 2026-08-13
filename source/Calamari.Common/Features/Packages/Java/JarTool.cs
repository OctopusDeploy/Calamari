using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Features.Packages.Java
{
    /// <summary>
    /// Wrapper class for invoking the Java Archive Tool http://docs.oracle.com/javase/7/docs/technotes/tools/windows/jar.html
    /// </summary>
    public class JarTool
    {
        readonly ICommandLineRunner commandLineRunner;
        readonly ILog log;
        readonly ICalamariFileSystem calamariFileSystem;
        readonly IVariables variables;
        readonly string toolsPath;

        public JarTool(ICommandLineRunner commandLineRunner, ILog log, ICalamariFileSystem calamariFileSystem, IVariables variables)
        {
            this.commandLineRunner = commandLineRunner;
            this.log = log;
            this.calamariFileSystem = calamariFileSystem;
            this.variables = variables;

            /*
                The precondition script will also set the location of the java library files
            */

            toolsPath = Path.Combine(
                variables?.Get(JavaVariables.JavaLibraryEnvVar, "") ?? "",
                "contentFiles",
                "any",
                "any",
                "tools.jar");
        }

        bool UseNativeZip => OctopusFeatureToggles.JavaArchiveNativeZipExtractionFeatureToggle.IsEnabled(variables);

        public void CreateJar(string contentsDirectory, string targetJarPath, bool enableCompression)
        {
            if (UseNativeZip)
            {
                CreateJarUsingNativeZip(contentsDirectory, targetJarPath, enableCompression);
                return;
            }

            CreateJarUsingJarTool(contentsDirectory, targetJarPath, enableCompression);
        }

        void CreateJarUsingJarTool(string contentsDirectory, string targetJarPath, bool enableCompression)
        {
            var compressionFlag = enableCompression ? "" : "0";
            var manifestPath = Path.Combine(contentsDirectory, "META-INF", "MANIFEST.MF");
            var args = File.Exists(manifestPath)
                ? $"-cp \"{toolsPath}\" sun.tools.jar.Main cvmf{compressionFlag} \"{manifestPath}\" \"{targetJarPath}\" -C \"{contentsDirectory}\" ."
                : $"-cp \"{toolsPath}\" sun.tools.jar.Main cvf{compressionFlag} \"{targetJarPath}\" -C \"{contentsDirectory}\" .";

            var createJarCommand = new CommandLineInvocation(JavaRuntime.CmdPath, args)
            {
                WorkingDirectory = contentsDirectory,
                OutputAsVerbose = true
            };
            log.Verbose($"Invoking '{createJarCommand}' to create '{targetJarPath}'");

            var result = commandLineRunner.Execute(createJarCommand);
            result.VerifySuccess();
        }

        /// <summary>
        /// Creates a Java archive file (.jar, .war, .ear) from the contents of a directory using .NET's
        /// built-in zip support instead of shelling out to the bundled JDK jar tool. A jar/war/ear is just
        /// a zip file with an optional manifest, so this does not need a JVM or the Octopus.Dependencies.Java
        /// tool package at all.
        /// </summary>
        void CreateJarUsingNativeZip(string contentsDirectory, string targetJarPath, bool enableCompression)
        {
            log.Verbose($"Creating '{targetJarPath}' from '{contentsDirectory}' using native zip creation");

            var compressionLevel = enableCompression ? CompressionLevel.Optimal : CompressionLevel.NoCompression;
            var manifestPath = Path.Combine(contentsDirectory, "META-INF", "MANIFEST.MF");

            if (File.Exists(targetJarPath))
                File.Delete(targetJarPath);

            using (var fileStream = new FileStream(targetJarPath, FileMode.CreateNew))
            using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create))
            {
                // The jar tool always writes a manifest, generating a default one when the caller
                // hasn't supplied their own (equivalent to `cvf` vs `cvmf`). Replicate that here so a
                // manifest-less contents directory still produces a valid jar.
                if (!File.Exists(manifestPath))
                {
                    var manifestEntry = archive.CreateEntry("META-INF/MANIFEST.MF", compressionLevel);
                    using (var writer = new StreamWriter(manifestEntry.Open()))
                    {
                        writer.NewLine = "\n";
                        writer.WriteLine("Manifest-Version: 1.0");
                        writer.WriteLine("Created-By: Octopus Deploy");
                        writer.WriteLine();
                    }
                }

                foreach (var filePath in Directory.EnumerateFiles(contentsDirectory, "*", SearchOption.AllDirectories)
                                                   .OrderBy(f => f, StringComparer.Ordinal))
                {
                    var relativePath = Path.GetRelativePath(contentsDirectory, filePath).Replace(Path.DirectorySeparatorChar, '/');
                    archive.CreateEntryFromFile(filePath, relativePath, compressionLevel);
                }
            }
        }

        /// <summary>
        /// Extracts a Java archive file (.jar, .war, .ear) to the target directory
        /// </summary>
        /// <returns>Count of files extracted</returns>
        public int ExtractJar(string jarPath, string targetDirectory)
        {
            return UseNativeZip
                ? ExtractJarUsingNativeZip(jarPath, targetDirectory)
                : ExtractJarUsingJarTool(jarPath, targetDirectory);
        }

        int ExtractJarUsingJarTool(string jarPath, string targetDirectory)
        {
            try
            {
                calamariFileSystem.EnsureDirectoryExists(targetDirectory);

                /*
                    Start by verifying the archive is valid.
                */
                var tfCommand = new CommandLineInvocation(
                    JavaRuntime.CmdPath,
                    $"-cp \"{toolsPath}\" sun.tools.jar.Main tf \"{jarPath}\""
                )
                {
                    WorkingDirectory = targetDirectory,
                    OutputAsVerbose = true
                };
                commandLineRunner.Execute(tfCommand).VerifySuccess();

                /*
                    If it is valid, go ahead an extract it
                */
                var extractJarCommand = new CommandLineInvocation(
                    JavaRuntime.CmdPath,
                    $"-cp \"{toolsPath}\" sun.tools.jar.Main xf \"{jarPath}\""
                )
                {
                    WorkingDirectory = targetDirectory,
                    OutputAsVerbose = true
                };

                log.Verbose($"Invoking '{extractJarCommand}' to extract '{jarPath}'");

                var result = commandLineRunner.Execute(extractJarCommand);
                result.VerifySuccess();
            }
            catch (Exception ex)
            {
                log.Error($"Exception thrown while extracting a Java archive. {ex}");
                throw;
            }

            return CountExtractedFiles(targetDirectory);
        }

        /// <summary>
        /// Extracts a Java archive file (.jar, .war, .ear) using .NET's built-in zip support instead of
        /// shelling out to the bundled JDK jar tool. A jar/war/ear is just a zip file with an optional
        /// manifest, so this does not need a JVM or the Octopus.Dependencies.Java tool package at all.
        /// </summary>
        int ExtractJarUsingNativeZip(string jarPath, string targetDirectory)
        {
            try
            {
                calamariFileSystem.EnsureDirectoryExists(targetDirectory);

                log.Verbose($"Extracting '{jarPath}' to '{targetDirectory}' using native zip extraction");

                ZipFile.ExtractToDirectory(jarPath, targetDirectory, overwriteFiles: true);
            }
            catch (Exception ex)
            {
                log.Error($"Exception thrown while extracting a Java archive. {ex}");
                throw;
            }

            return CountExtractedFiles(targetDirectory);
        }

        int CountExtractedFiles(string targetDirectory)
        {
            try
            {
                return Directory.EnumerateFiles(targetDirectory, "*", SearchOption.AllDirectories).Count();
            }
            catch (Exception ex)
            {
                log.Verbose(
                    $"Unable to return extracted file count. Error while enumerating '{targetDirectory}':\n{ex.Message}");
                return -1;
            }
        }
    }
}
