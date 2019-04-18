using System;
using System.IO;
using System.Linq;
using Calamari.Deployment;
using Calamari.Integration.Processes;
using Octostache;

namespace Calamari.Integration.Packages.Java
{
    /// <summary>
    /// Wrapper class for invoking the Java Archive Tool http://docs.oracle.com/javase/7/docs/technotes/tools/windows/jar.html
    /// </summary>
    public class JarTool
    {
        readonly ICommandLineRunner commandLineRunner;
        readonly ICommandOutput commandOutput;
        readonly string toolsPath;

        public JarTool(ICommandLineRunner commandLineRunner, ICommandOutput commandOutput, VariableDictionary variables)
        {
            this.commandLineRunner = commandLineRunner;
            this.commandOutput = commandOutput;

            /*
                The precondition script will also set the location of the java libray files
            */
            
            toolsPath = Path.Combine(
                variables?.Get(SpecialVariables.Action.Java.JavaLibraryEnvVar,"") ?? "",
                "contentFiles",
                "any",
                "any",
                "tools.jar");
        }

        public void CreateJar(string contentsDirectory, string targetJarPath)
        {
            try
            {
                var manifestPath = Path.Combine(contentsDirectory, "META-INF", "MANIFEST.MF");
                var args = File.Exists(manifestPath)
                    ? $"-cp \"{toolsPath}\" sun.tools.jar.Main cvmf \"{manifestPath}\" \"{targetJarPath}\" -C \"{contentsDirectory}\" ."
                    : $"-cp \"{toolsPath}\" sun.tools.jar.Main cvf \"{targetJarPath}\" -C \"{contentsDirectory}\" .";

                var createJarCommand = new CommandLineInvocation(JavaRuntime.CmdPath, args, contentsDirectory);
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

            try
            {
                /*
                     All extraction messages should be verbose
                 */
                commandOutput.WriteInfo("##octopus[stdout-verbose]");

                /*
                    Start by verifying the archive is valid.
                */
                commandLineRunner.Execute(new CommandLineInvocation(
                    JavaRuntime.CmdPath,
                    $"-cp \"{toolsPath}\" sun.tools.jar.Main tf \"{jarPath}\"",
                    targetDirectory)).VerifySuccess();

                /*
                    If it is valid, go ahead an extract it
                */
                var extractJarCommand = new CommandLineInvocation(
                    JavaRuntime.CmdPath,
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
            catch (Exception ex)
            {
                commandOutput.WriteError($"Exception thrown while extracting a Java archive. {ex}");
                throw ex;
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
    }
}