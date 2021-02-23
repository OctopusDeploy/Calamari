using System.IO;
using System.Runtime.InteropServices;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Tests.Helpers
{
    public class CodeGenerator
    {
        public static string GenerateConsoleApplication(string projectName, string destinationFolder)
        {
            var projectPath = Directory.CreateDirectory(Path.Combine(destinationFolder, projectName));

            CommandLineInvocation CreateCommandLineInvocation(string executable, string arguments)
            {
                return new CommandLineInvocation(executable, arguments)
                {
                    OutputToLog = false,
                    WorkingDirectory = projectPath.FullName
                };
            }

            var clr = new CommandLineRunner(ConsoleLog.Instance, new CalamariVariables());
            var result = clr.Execute(CreateCommandLineInvocation("dotnet", "new console -f netcoreapp3.1"));
            result.VerifySuccess();
            File.WriteAllText(Path.Combine(projectPath.FullName, "global.json"),
                              @"{
    ""sdk"": {
            ""version"": ""3.1.402"",
            ""rollForward"": ""latestFeature""
        }
    }");
            var programCS = Path.Combine(projectPath.FullName, "Program.cs");
            var newProgram = $@"using System;
class Program
{{
    static void Main(string[] args)
    {{
        Console.WriteLine($""Hello from my custom {projectName}!"");
        Console.Write(String.Join(Environment.NewLine, args));
    }}
}}";

            var architecture = RuntimeInformation.ProcessArchitecture;
            var rid = "win-x64";
            if (CalamariEnvironment.IsRunningOnMac)
            {
                rid = "osx-x64";
            }
            else if (CalamariEnvironment.IsRunningOnNix)
            {
                rid = "linux-x64";
            }

            if (architecture == Architecture.Arm)
            {
                rid = "linux-arm";
            }

            if (architecture == Architecture.Arm64)
            {
                rid = "linux-arm64";
            }

            File.WriteAllText(programCS, newProgram);
            var outputPath = Path.Combine(projectPath.FullName, "output");
            result = clr.Execute(CreateCommandLineInvocation("dotnet", $"publish -o {outputPath} -r {rid}"));
            result.VerifySuccess();

            return outputPath;
        }
    }
}