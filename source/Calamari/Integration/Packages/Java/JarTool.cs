using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;

namespace Calamari.Integration.Packages.Java
{
    /// <summary>
    /// Wrapper class for invoking the Java Archive Tool http://docs.oracle.com/javase/7/docs/technotes/tools/windows/jar.html  
    /// </summary>
    public class JarTool
    {
        readonly ICommandLineRunner commandLineRunner;
        readonly ICalamariFileSystem fileSystem;

        public JarTool(ICommandLineRunner commandLineRunner, ICalamariFileSystem fileSystem)
        {
            this.commandLineRunner = commandLineRunner;
            this.fileSystem = fileSystem;
        }

        public void CreateJar(string contentsDirectory, string targetJarPath)
        {
            /*
                 The precondition script will set the OctopusEnvironment_Java_Bin environment variable based
                 on where it found the java executable based on the JAVA_HOME environment
                 variable. If OctopusEnvironment_Java_Bin is empty or null, it means that the precondition
                 found java on the path.
             */
            var javaBin = Environment.GetEnvironmentVariable("OctopusEnvironment_Java_Bin") ?? "";
            var createJarCommand = new CommandLineInvocation(
               $"{javaBin}java", 
               $"-cp tools.jar sun.tools.jar.Main cvf \"{targetJarPath}\" -C \"{contentsDirectory}\" .", 
               contentsDirectory);

            Log.Verbose($"Invoking '{createJarCommand}' to create '{targetJarPath}'");
            var result = commandLineRunner.Execute(createJarCommand);
            result.VerifySuccess();
        }

        /// <summary>
        /// Extracts a Java archive file (.jar, .war, .ear) to the target directory
        /// </summary>
        /// <returns>Count of files extracted</returns>
        public int ExtractJar(string jarPath, string targetDirectory)
        {
            /*
                 The precondition script will set the OctopusEnvironment_Java_Bin environment variable based
                 on where it found the java executable based on the JAVA_HOME environment
                 variable. If OctopusEnvironment_Java_Bin is empty or null, it means that the precondition
                 found java on the path.
             */
            var javaBin = Environment.GetEnvironmentVariable("OctopusEnvironment_Java_Bin") ?? "";
            var extractJarCommand = new CommandLineInvocation(
                $"{javaBin.Trim()}java", 
                $"-cp tools.jar sun.tools.jar.Main xf \"{jarPath}\"", 
                targetDirectory);

            Log.Verbose($"Invoking '{extractJarCommand}' to extract '{jarPath}'");
            var result = commandLineRunner.Execute(extractJarCommand);
            result.VerifySuccess();

            var count = -1;

            try
            {
                count = Directory.EnumerateFiles(targetDirectory, "*", SearchOption.AllDirectories).Count(); 
            }
            catch (Exception ex)
            {
                Log.Verbose(
                    $"Unable to return extracted file count. Error while enumerating '{targetDirectory}':\n{ex.Message}");
            }

            return count;
        }

        /// <summary>
        /// Extracts the contents of the JAR's manifest file
        /// </summary>
        public string ExtractManifest(string jarPath)
        {
            const string manifestJarPath = "META-INF/MANIFEST.MF";

            var tempDirectory = fileSystem.CreateTemporaryDirectory();

            var extractJarCommand =
                new CommandLineInvocation("java", $"-cp tools.jar sun.tools.jar.Main xf \"{jarPath}\" \"{manifestJarPath}\"", tempDirectory);

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