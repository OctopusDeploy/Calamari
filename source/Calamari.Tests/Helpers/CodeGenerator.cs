using System.IO;
using Calamari.Common.Features.Processes;
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
            File.WriteAllText(programCS, newProgram);
            var outputPath = Path.Combine(projectPath.FullName, "output");
            result = clr.Execute(CreateCommandLineInvocation("dotnet", $"build -o {outputPath}"));
            result.VerifySuccess();

            return outputPath;
        }
    }
}