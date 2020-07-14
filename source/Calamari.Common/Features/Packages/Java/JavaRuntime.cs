using System;
using System.IO;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;

namespace Calamari.Common.Features.Packages.Java
{
    public static class JavaRuntime
    {
        static string ExecutingDirectory => Path.GetDirectoryName(typeof(JavaRuntime).Assembly.Location);

        public static string CmdPath
        {
            get
            {
                var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
                return string.IsNullOrEmpty(javaHome) ? "java" : Path.Combine(javaHome, "bin", "java");
            }
        }

        public static void VerifyExists()
        {
            const string minimumJavaVersion = "1.8";
            var jarFile = Path.Combine(ExecutingDirectory, "javatest.jar");
            try
            {
                var silentProcessResult = SilentProcessRunner.ExecuteCommand(CmdPath,
                    $"-jar \"{jarFile}\" {minimumJavaVersion}",
                    ".",
                    Console.WriteLine,
                    i => Console.Error.WriteLine(i));

                if (silentProcessResult.ExitCode == 0)
                    return;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            throw new CommandException(
                $"Failed to run {CmdPath}. You must have Java {minimumJavaVersion} or later installed on the target machine, " +
                "and have the java executable on the path or have the JAVA_HOME environment variable defined");
        }
    }
}