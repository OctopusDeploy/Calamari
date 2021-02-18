using System;
using System.IO;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.LaunchTools;
using Calamari.Serialization;
using Calamari.Tests.Helpers;
using Newtonsoft.Json;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.Fixtures.Manifest
{
    [TestFixture]
    public class ExecuteManifestCommandFixture : CalamariFixture
    {
        [Test]
        public void NoManifestFile()
        {
            var variables = new VariableDictionary();

            var result = ExecuteCommand(variables);

            result.AssertFailure();
            result.AssertErrorOutput("Execution manifest not found in variables.");
        }

        [Test]
        public void NoInstructions()
        {
            var variables = new VariableDictionary
            {
                { SpecialVariables.Execution.Manifest, "[]" }
            };

            var result = ExecuteCommand(variables);

            result.AssertFailure();
            result.AssertErrorOutput("The execution manifest must have at least one instruction.");
        }

        [Test]
        public void WithInstructions()
        {
            void CopyDirectory(string sourcePath, string destPath)
            {
                if (!Directory.Exists(destPath))
                {
                    Directory.CreateDirectory(destPath);
                }

                foreach (var file in Directory.EnumerateFiles(sourcePath))
                {
                    var dest = Path.Combine(destPath, Path.GetFileName(file));
                    File.Copy(file, dest);
                }

                foreach (var folder in Directory.EnumerateDirectories(sourcePath))
                {
                    var dest = Path.Combine(destPath, Path.GetFileName(folder));
                    CopyDirectory(folder, dest);
                }
            }

            var instructions = new[]
            {
                new Instruction
                {
                    Launcher = LaunchTools.LaunchTools.Calamari,
                    LauncherInstructions = JsonConvert.SerializeObject(new CalamariInstructions
                                                                       {
                                                                           Command = "test-calamari-instruction"
                                                                       },
                                                                       JsonSerialization.GetDefaultSerializerSettings())
                },
                new Instruction
                {
                    Launcher = LaunchTools.LaunchTools.Node,
                    LauncherInstructions = JsonConvert.SerializeObject(new NodeInstructions
                                                                       {
                                                                           BootstrapperPathVariable = "BootstrapperPathVariable",
                                                                           NodePathVariable = "NodePathVariable",
                                                                           TargetEntryPoint = "TargetEntryPoint",
                                                                           TargetPathVariable = "TargetPathVariable"
                                                                       },
                                                                       JsonSerialization.GetDefaultSerializerSettings())
                }
            };

            var instructionsJson = JsonConvert.SerializeObject(instructions, JsonSerialization.GetDefaultSerializerSettings());
            using (var temporaryDirectory = TemporaryDirectory.Create())
            {
                string destinationPath;
                var installationPath = destinationPath = Path.Combine(temporaryDirectory.DirectoryPath, "app");
                Directory.CreateDirectory(installationPath);
                var outputPath = GenerateCode("node", temporaryDirectory.DirectoryPath);
                if (!CalamariEnvironment.IsRunningOnWindows)
                {
                    destinationPath = Path.Combine(installationPath, "bin");
                    Directory.CreateDirectory(destinationPath);
                }

                CopyDirectory(outputPath, destinationPath);

                var variables = new VariableDictionary
                {
                    { SpecialVariables.Execution.Manifest, instructionsJson },
                    { nameof(NodeInstructions.BootstrapperPathVariable), "BootstrapperPathVariable_Value" },
                    { nameof(NodeInstructions.NodePathVariable), installationPath },
                    { nameof(NodeInstructions.TargetEntryPoint), "TargetEntryPoint_Value" },
                    { nameof(NodeInstructions.TargetPathVariable), "TargetPathVariable_Value" },
                };


                var result = ExecuteCommand(variables, "Calamari.Tests");

                result.AssertSuccess();
                result.AssertOutput(string.Join(Environment.NewLine, "Hello from TestCommand", "Hello from my custom Node!",
                                                $"BootstrapperPathVariable_Value{Path.PathSeparator}bootstrapper.js",
                                                $"TargetPathVariable_Value{Path.PathSeparator}TargetEntryPoint"));
            }
        }

        static string GenerateCode(string projectName, string destinationFolder)
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
            var newProgram = @"using System;
class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine(""Hello from my custom Node!"");
        Console.Write(String.Join(Environment.NewLine, args));
    }
}";
            File.WriteAllText(programCS, newProgram);
            result = clr.Execute(CreateCommandLineInvocation("dotnet", "build"));
            result.VerifySuccess();

            var outputPath = Path.Combine(projectPath.FullName,
                                       "bin",
                                       "Debug",
                                       "netcoreapp3.1");

            return outputPath;
        }

        CalamariResult ExecuteCommand(VariableDictionary variables, string extensions = "")
        {
            using (var variablesFile = new TemporaryFile(Path.GetTempFileName()))
            {
                variables.Save(variablesFile.FilePath);

                return Invoke(Calamari()
                              .Action("execute-manifest")
                              .Argument("variables", variablesFile.FilePath)
                              .Argument("sensitiveVariablesPassword", "GB8KdBqYRlgAON9ISUPdnQ==")
                              .Argument("extensions", extensions));
            }
        }
    }

    [Command("test-calamari-instruction")]
    public class TestCommand : Command
    {
        readonly ILog log;

        public TestCommand(ILog log)
        {
            this.log = log;
        }
        public override int Execute(string[] commandLineArguments)
        {
            log.Info("Hello from TestCommand");

            return 0;
        }
    }
}