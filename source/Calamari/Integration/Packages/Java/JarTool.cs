using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Deployment;
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
        private readonly ICommandOutput commandOutput;
        private readonly string toolsPath;

        public JarTool(ICommandLineRunner commandLineRunner, ICommandOutput commandOutput, ICalamariFileSystem fileSystem)
        {
            this.commandLineRunner = commandLineRunner;
            this.fileSystem = fileSystem;
            this.commandOutput = commandOutput;
            
            /*
                The precondition script will also set the location of the java libray files
            */
            toolsPath = Path.Combine(
                Environment.GetEnvironmentVariable(SpecialVariables.Action.Java.JavaLibraryEnvVar) ?? "", 
                "contentFiles", 
                "any", 
                "any",
                "tools.jar");
        }

        public void CreateJar(string contentsDirectory, string targetJarPath)
        {
            try
            {
                /*
                     The precondition script will set the OctopusEnvironment_Java_Bin environment variable based
                     on where it found the java executable based on the JAVA_HOME environment
                     variable. If OctopusEnvironment_Java_Bin is empty or null, it means that the precondition
                     found java on the path.
                 */
                var javaBin = Environment.GetEnvironmentVariable(SpecialVariables.Action.Java.JavaBinEnvVar) ?? "";
                var createJarCommand = new CommandLineInvocation(
                    Path.Combine(javaBin, "java"),
                    $"-cp \"{toolsPath}\" sun.tools.jar.Main cvf \"{targetJarPath}\" -C \"{contentsDirectory}\" .",
                    contentsDirectory);

                Log.Verbose($"Invoking '{createJarCommand}' to create '{targetJarPath}'");
                
                /*
                     All extraction messages should be verbose
                 */
                commandOutput.WriteInfo("##octopus[stdout-verbose]");                

                var result = commandLineRunner.Execute(createJarCommand);
                result.VerifySuccess();
            }
            finally
            {
                commandOutput.WriteInfo("##octopus[stdout-default]");
            }
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
            var javaBin = Environment.GetEnvironmentVariable(SpecialVariables.Action.Java.JavaBinEnvVar) ?? "";

            try
            {
                /*
                     All extraction messages should be verbose
                 */
                commandOutput.WriteInfo("##octopus[stdout-verbose]"); 
                
                /*
                    Start by verifiying the archive is valid.
                */
                commandLineRunner.Execute(new CommandLineInvocation(
                    Path.Combine(javaBin, "java"),
                    $"-cp \"{toolsPath}\" sun.tools.jar.Main tf \"{jarPath}\"",
                    targetDirectory)).VerifySuccess();

                /*
                    If it is valid, go ahead an extract it
                */
                var extractJarCommand = new CommandLineInvocation(
                        Path.Combine(javaBin, "java"),
                        $"-cp \"{toolsPath}\" sun.tools.jar.Main xf \"{jarPath}\"",
                        targetDirectory);
                
                Log.Verbose($"Invoking '{extractJarCommand}' to extract '{jarPath}'");     
                
                /*
                     All extraction messages should be verbose
                 */
                commandOutput.WriteInfo("##octopus[stdout-verbose]");                
            
                var result = commandLineRunner.Execute(extractJarCommand);
                result.VerifySuccess();
            }
            finally
            {
                commandOutput.WriteInfo("##octopus[stdout-default]");
            }

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
                new CommandLineInvocation("java", $"-cp \"{toolsPath}\" sun.tools.jar.Main xf \"{jarPath}\" \"{manifestJarPath}\"", tempDirectory);

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