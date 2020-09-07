using System;
using System.IO;
using System.Linq;
using Calamari.Common.Features.Processes;
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
        readonly string toolsPath;

        public JarTool(ICommandLineRunner commandLineRunner, ILog log, IVariables variables)
        {
            this.commandLineRunner = commandLineRunner;
            this.log = log;

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

        public void CreateJar(string contentsDirectory, string targetJarPath, bool enableCompression)
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
        /// Extracts a Java archive file (.jar, .war, .ear) to the target directory
        /// </summary>
        /// <returns>Count of files extracted</returns>
        public int ExtractJar(string jarPath, string targetDirectory)
        {
            try
            {
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

            var count = -1;

            try
            {
                count = Directory.EnumerateFiles(targetDirectory, "*", SearchOption.AllDirectories).Count();
            }
            catch (Exception ex)
            {
                log.Verbose(
                    $"Unable to return extracted file count. Error while enumerating '{targetDirectory}':\n{ex.Message}");
            }

            return count;
        }
    }
}