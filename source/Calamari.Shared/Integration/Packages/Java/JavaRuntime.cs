using System;
using System.IO;
using Calamari.Commands.Support;
using Calamari.Integration.Processes;

namespace Calamari.Integration.Packages.Java
{
    public static class JavaRuntime
    {
        static string GetExecutingDirectory()
        {
            var location = new Uri(typeof(JavaRuntime).Assembly.CodeBase);
            return Path.GetDirectoryName(location.AbsolutePath);
        }

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
            var MinimumJavaVersion = "1.8";
            var jarFile = Path.Combine(GetExecutingDirectory(), "javatest.jar");
            var silentProcessResult = SilentProcessRunner.ExecuteCommand(CmdPath,
                $"-jar \"{jarFile}\" {MinimumJavaVersion}", ".", Console.WriteLine, (i) => Console.Error.WriteLine(i));

            if (silentProcessResult.ExitCode != 0)
            {
                throw new CommandException(
                    $"You must have Java {MinimumJavaVersion} or later installed on the target machine, " +
                    "and have the java executable on the path or have the JAVA_HOME environment variable defined");
            }
        }
    }
}