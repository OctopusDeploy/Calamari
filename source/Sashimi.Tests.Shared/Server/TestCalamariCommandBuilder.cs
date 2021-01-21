using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Calamari;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing;
using System.Threading.Tasks;
using Calamari.Common.Features.Packages.NuGet;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Tests.Helpers;
using Calamari.Tests.Shared;
using Calamari.Tests.Shared.Helpers;
using Calamari.Tests.Shared.LogParser;
using Sashimi.Server.Contracts.ActionHandlers;
using Sashimi.Server.Contracts.Calamari;
using Sashimi.Server.Contracts.CommandBuilders;
using Sashimi.Server.Contracts.DeploymentTools;
using Octopus.CoreUtilities;
using Sashimi.Server.Contracts.Actions;
using Sashimi.Tests.Shared.Extensions;

namespace Sashimi.Tests.Shared.Server
{
    class TestCalamariCommandBuilder<TCalamariProgram> : ICalamariCommandBuilder
    {
        const string CalamariBinariesLocationEnvironmentVariable = "CalamariBinaries_RelativePath";

        public static class InProcOutProcOverride
        {
            public static readonly string EnvironmentVariable = "Test_Calamari_InProc_OutProc_Override";
            public static readonly string InProcValue = "InProc";
            public static readonly string OutProcValue = "OutProc";
        }

        public TestCalamariCommandBuilder(CalamariFlavour calamariFlavour, string calamariCommand)
        {
            CalamariFlavour = calamariFlavour;
            CalamariCommand = calamariCommand;
        }

        TestVariableDictionary variables = new TestVariableDictionary();
        bool withStagedPackageArgument;

        public CalamariFlavour CalamariFlavour { get; set; }
        public string CalamariCommand { get; set; }
        public List<(string? filename, Stream contents)> Files = new List<(string?, Stream)>();
        public List<(string name, string? value)> Arguments = new List<(string, string?)>();

        public IList<IDeploymentTool> Tools { get; } = new List<IDeploymentTool>();

        public ICalamariCommandBuilder WithStagedPackageArgument()
        {
            withStagedPackageArgument = true;
            return this;
        }

        public ICalamariCommandBuilder WithArgument(string name)
        {
            throw new NotImplementedException();
        }

        public ICalamariCommandBuilder WithArgument(string name, string? value)
        {
            throw new NotImplementedException();
        }

        public ICalamariCommandBuilder WithExtension(string extension)
        {
            throw new NotImplementedException();
        }

        public ICalamariCommandBuilder WithDataFile(string fileContents, string? fileName = null)
        {
            WithDataFile(fileContents.EncodeInUtf8Bom(), fileName);
            return this;
        }

        public ICalamariCommandBuilder WithDataFileNoBom(string fileContents, string? fileName = null)
        {
            WithDataFile(fileContents.EncodeInUtf8NoBom(), fileName);
            return this;
        }

        public ICalamariCommandBuilder WithDataFile(byte[] fileContents, string? fileName = null)
        {
            WithDataFile(new MemoryStream(fileContents), fileName);
            return this;
        }

        public ICalamariCommandBuilder WithDataFile(Stream fileContents, string? fileName = null, Action<int>? progress = null)
        {
            Files.Add((fileName, fileContents));
            return this;
        }

        public ICalamariCommandBuilder WithDataFileAsArgument(string argumentName, string fileContents, string? fileName = null)
            => throw new NotImplementedException();

        public ICalamariCommandBuilder WithDataFileAsArgument(string argumentName, byte[] fileContents, string? fileName = null)
            => throw new NotImplementedException();

        public ICalamariCommandBuilder WithTool(IDeploymentTool tool)
        {
            Tools.Add(tool);
            return this;
        }

        public ICalamariCommandBuilder WithVariable(string name, string? value, bool isSensitive = false)
        {
            throw new NotImplementedException();
        }

        public ICalamariCommandBuilder WithVariable(string name, bool value, bool isSensitive = false)
            => throw new NotImplementedException();

        public ICalamariCommandBuilder WithIsolation(ExecutionIsolation executionIsolation)
            => throw new NotImplementedException();

        public ICalamariCommandBuilder WithIsolationTimeout(TimeSpan mutexTimeout)
            => throw new NotImplementedException();

        public string Describe()
            => throw new NotImplementedException();

        public IActionHandlerResult Execute()
        {
            using var working = TemporaryDirectory.Create();
            var workingPath = working.DirectoryPath;

            //HACK: set the working directory, we will need to modify Calamari to not depend on this
            var originalWorkingDirectory = Environment.CurrentDirectory;
            try
            {
                Environment.CurrentDirectory = workingPath;

                using var toolsBasePath = TemporaryDirectory.Create();
                var paths = InstallTools(toolsBasePath.DirectoryPath);

                var args = GetArgs(workingPath);

                CopyFilesToWorkingFolder(workingPath);

                return ExecuteActionHandler(args, paths);
            }
            finally
            {
                Environment.CurrentDirectory = originalWorkingDirectory;
            }
        }

        List<string> InstallTools(string toolsPath)
        {
            if (!PlatformDetection.IsRunningOnWindows)
            {
                return new List<string>();
            }

            var extractor = new NupkgExtractor(new InMemoryLog());

            var modulePaths = new List<string>();
            var addToPath = new List<string>();
            var platform = KnownPlatforms.Windows;

            if (PlatformDetection.IsRunningOnNix)
            {
                platform = KnownPlatforms.Linux64;
            }
            if (PlatformDetection.IsRunningOnMac)
            {
                platform = KnownPlatforms.Osx64;
            }

            foreach (var tool in Tools)
            {
                var toolPath = Path.Combine(toolsPath, tool.Id);
                modulePaths.AddRange(tool.GetCompatiblePackage(platform)
                                         .SelectValueOr(package => package.BootstrapperModulePaths, Enumerable.Empty<string>())
                                         .Select(s => Path.Combine(toolPath, s)));

                var toolPackagePath = Path.Combine(Path.GetDirectoryName(AssemblyExtensions.FullLocalPath(Assembly.GetExecutingAssembly())), $"{tool.Id}.nupkg");
                if (!File.Exists(toolPackagePath))
                {
                    throw new Exception($"{tool.Id}.nupkg missing.");
                }

                extractor.Extract(toolPackagePath, toolPath);
                var fullPathToTool = tool.SubFolder.None()
                    ? toolPath
                    : Path.Combine(toolPath, tool.SubFolder.Value);
                if (tool.ToolPathVariableToSet.Some())
                {
                    variables[tool.ToolPathVariableToSet.Value] = fullPathToTool
                                                                  .Replace("$HOME", "#{env:HOME}")
                                                                  .Replace("$TentacleHome", "#{env:TentacleHome}");
                }

                if (tool.AddToPath)
                {
                    addToPath.Add(fullPathToTool);
                }
            }

            var modules = string.Join(";", modulePaths);
            variables["Octopus.Calamari.Bootstrapper.ModulePaths"] = modules;

            return addToPath;
        }

        List<string> GetArgs(string workingPath)
        {
            var args = new List<string> {CalamariCommand};

            args.AddRange(
                Arguments
                    .Select(a => $"--{a.name}{(a.value == null ? "" : $"={a.value}")}")
            );

            var varPath = Path.Combine(workingPath, "variables.json");

            variables.Save(varPath);
            args.Add($"--variables={varPath}");

            return args;
        }

        void CopyFilesToWorkingFolder(string workingPath)
        {
            foreach (var (filename, contents) in Files)
            {
                using var fileStream = File.Create(Path.Combine(workingPath, filename!));
                contents.Seek(0, SeekOrigin.Begin);
                contents.CopyTo(fileStream);
            }

            if (!withStagedPackageArgument)
            {
                var packagePath = variables.GetRaw("Octopus.Test.PackagePath");

                if (packagePath != null)
                {
                    if (File.Exists(packagePath))
                    {
                        var fileName = new FileInfo(packagePath).Name;
                        File.Copy(packagePath, Path.Combine(workingPath, fileName));
                    }
                    else if (Directory.Exists(packagePath))
                    {
                        Copy(packagePath, workingPath);
                    }
                }
            }
        }

        IActionHandlerResult ExecuteActionHandler(List<string> args, List<string> paths)
        {
            var inProcOutProcOverride = Environment.GetEnvironmentVariable(InProcOutProcOverride.EnvironmentVariable);
            if (!string.IsNullOrEmpty(inProcOutProcOverride))
            {
                if (InProcOutProcOverride.InProcValue.Equals(inProcOutProcOverride, StringComparison.OrdinalIgnoreCase))
                    return ExecuteActionHandlerInProc(args, paths).GetAwaiter().GetResult();

                if (InProcOutProcOverride.OutProcValue.Equals(inProcOutProcOverride, StringComparison.OrdinalIgnoreCase))
                    return ExecuteActionHandlerOutProc(args, paths);

                throw new Exception($"'{InProcOutProcOverride.EnvironmentVariable}' environment variable must be '{InProcOutProcOverride.InProcValue}' or '{InProcOutProcOverride.OutProcValue}'");
            }

            if (TestEnvironment.IsCI)
                return ExecuteActionHandlerOutProc(args, paths);

            return ExecuteActionHandlerInProc(args, paths).GetAwaiter().GetResult();
        }

        async Task<IActionHandlerResult> ExecuteActionHandlerInProc(List<string> args, List<string> paths)
        {
            Console.WriteLine("Running Calamari InProc");
            AssertMatchingCalamariFlavour();

            var inMemoryLog = new InMemoryLog();
            var constructor = typeof(TCalamariProgram).GetConstructor(
                BindingFlags.Public | BindingFlags.Instance,
                null, new[] {typeof(ILog)}, new ParameterModifier[0]);
            if (constructor == null)
            {
                throw new Exception(
                    $"{typeof(TCalamariProgram).Name} doesn't seem to have a `public {typeof(TCalamariProgram)}({nameof(ILog)})` constructor.");
            }

            var instance = (TCalamariProgram) constructor.Invoke(new object?[]
            {
                inMemoryLog
            });

            var methodInfo =
                typeof(TCalamariProgram).GetMethod("Run", BindingFlags.Instance | BindingFlags.NonPublic);
            if (methodInfo == null)
            {
                throw new Exception($"{typeof(TCalamariProgram).Name}.Run method was not found.");
            }

            var exitCode = await ExecuteWrapped(paths,
                                                async () =>
                                                {
                                                    if (methodInfo.ReturnType.IsGenericType)
                                                    {
                                                        return await (Task<int>)methodInfo.Invoke(instance, new object?[] { args.ToArray() });
                                                    }

                                                    return (int)methodInfo.Invoke(instance, new object?[] { args.ToArray() });
                                                });

            var serverInMemoryLog = new ServerInMemoryLog();

            var outputFilter = new ScriptOutputFilter(serverInMemoryLog);
            foreach (var text in inMemoryLog.StandardError)
            {
                outputFilter.Write(ProcessOutputSource.StdErr, text);
            }

            foreach (var text in inMemoryLog.StandardOut)
            {
                outputFilter.Write(ProcessOutputSource.StdOut, text);
            }

            return new TestActionHandlerResult(exitCode,
                outputFilter.TestOutputVariables, outputFilter.Actions,
                outputFilter.ServiceMessages, outputFilter.ResultMessage, outputFilter.Artifacts,
                serverInMemoryLog.ToString());
        }

        Task<int> ExecuteWrapped(IReadOnlyCollection<string> paths, Func<Task<int>> func)
        {
            if (paths.Count > 0)
            {
                var originalPath = Environment.GetEnvironmentVariable("PATH");
                try
                {
                    Environment.SetEnvironmentVariable("PATH", $"{originalPath};{String.Join(";", paths)}", EnvironmentVariableTarget.Process);

                    return func();
                }
                finally
                {
                    Environment.SetEnvironmentVariable("PATH", originalPath, EnvironmentVariableTarget.Process);
                }
            }

            return func();
        }

        IActionHandlerResult ExecuteActionHandlerOutProc(List<string> args, List<string> paths)
        {
            using (var variablesFile = new TemporaryFile(Path.GetTempFileName()))
            {
                variables.Save(variablesFile.FilePath);

                var calamariFullPath = GetOutProcCalamariExePath();
                Console.WriteLine("Running Calamari OutProc from: "+ calamariFullPath);

                var commandLine = CreateCommandLine(calamariFullPath);
                foreach (var argument in args)
                    commandLine = commandLine.Argument(argument);

                var calamariResult = Invoke(commandLine, paths);

                var capturedOutput = calamariResult.CapturedOutput;

                var serverInMemoryLog = new ServerInMemoryLog();

                var outputFilter = new ScriptOutputFilter(serverInMemoryLog);
                foreach (var text in capturedOutput.Errors)
                {
                    outputFilter.Write(ProcessOutputSource.StdErr, text);
                }

                foreach (var text in capturedOutput.Infos)
                {
                    outputFilter.Write(ProcessOutputSource.StdOut, text);
                }

                return new TestActionHandlerResult(calamariResult.ExitCode,
                    outputFilter.TestOutputVariables, outputFilter.Actions,
                    outputFilter.ServiceMessages, outputFilter.ResultMessage, outputFilter.Artifacts,
                    serverInMemoryLog.ToString());
            }
        }

        static CommandLine CreateCommandLine(string calamariFullPath)
        {
            if (!CalamariEnvironment.IsRunningOnWindows && calamariFullPath.EndsWith(".exe"))
            {
                var commandLine = new CommandLine("mono");
                commandLine.Argument(calamariFullPath);
                return commandLine;
            }

            ExecutableHelper.AddExecutePermission(calamariFullPath);
            return new CommandLine(calamariFullPath);
        }

        string GetOutProcCalamariExePath()
        {
            var calamariFlavour = typeof(TCalamariProgram).Assembly.GetName().Name;
            var sashimiTestFolder = Path.GetDirectoryName(AssemblyExtensions.FullLocalPath(typeof(TCalamariProgram).Assembly));

            if (TestEnvironment.IsCI)
            {
                //This is where Teamcity puts the Calamari binaries
                var calamaribinariesFolder = Environment.GetEnvironmentVariable(CalamariBinariesLocationEnvironmentVariable);
                if (string.IsNullOrEmpty(calamaribinariesFolder))
                    throw new ApplicationException($"It appears that the environment variable {CalamariBinariesLocationEnvironmentVariable} is not set. Without this, I cant find the binaries!");
                return AddExeExtensionIfNecessary(Path.GetFullPath(Path.Combine(sashimiTestFolder, calamaribinariesFolder, calamariFlavour)));
            }

            //Running locally - change these to your liking
            var configuration = "Debug"; //Debug;Release
            var targetFramework = "net452"; //net452;netcoreapp3.1
            var runtime = "win-x64"; //win-x64;linux-x64;osx-x64;linux-arm;linux-arm64

            //When running out of process locally, always publish so we get something runnable for NetCore
            var calamariProjectFolder = Path.GetFullPath(Path.Combine(sashimiTestFolder, "../../../..", calamariFlavour));
            DotNetPublish(calamariProjectFolder, configuration, targetFramework, runtime);

            return AddExeExtensionIfNecessary(Path.Combine(calamariProjectFolder, "bin", "Debug", targetFramework, runtime, "publish", calamariFlavour));

            void DotNetPublish(string calamariProjectFolder, string configuration, string targetFramework, string runtime)
            {
                var stdOut = new StringBuilder();
                var stdError = new StringBuilder();
                var result = SilentProcessRunner.ExecuteCommand($"dotnet",
                    $"publish --framework {targetFramework} --configuration {configuration} --runtime {runtime}",
                    calamariProjectFolder,
                    s => stdOut.AppendLine(s),
                    s => stdError.AppendLine(s));

                if (result.ExitCode != 0)
                    throw new Exception(stdOut.ToString() + stdError);
            }

            string AddExeExtensionIfNecessary(string exePath)
            {
                if (File.Exists(exePath))
                    return exePath;

                var withExeExtension = exePath + ".exe";
                if (File.Exists(withExeExtension))
                    return withExeExtension;

                throw new Exception($"Calamari exe doesn't exist on disk: '{exePath}(.exe)'");
            }
        }

        CalamariResult Invoke(CommandLine command, List<string> paths)
        {
            var runner = new TestCommandLineRunner(ConsoleLog.Instance, new CalamariVariables());
            var commandLineInvocation = command.Build();
            if (paths.Count > 0)
            {
                commandLineInvocation.EnvironmentVars = new Dictionary<string, string>
                {
                    { "PATH", $"{Environment.GetEnvironmentVariable("PATH")};{String.Join(";", paths)}" }
                };
            }

            var result = runner.Execute(commandLineInvocation);
            return new CalamariResult(result.ExitCode, runner.Output);
        }

        void Copy(string sourcePath, string destinationPath)
        {
            foreach (var dirPath in Directory.EnumerateDirectories(sourcePath, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dirPath.Replace(sourcePath, destinationPath));
            }

            foreach (var newPath in Directory.EnumerateFiles(sourcePath, "*.*", SearchOption.AllDirectories))
            {
                File.Copy(newPath, newPath.Replace(sourcePath, destinationPath), true);
            }
        }

        void AssertMatchingCalamariFlavour()
        {
            var assemblyName = typeof(TCalamariProgram).Assembly.GetName();
            if (CalamariFlavour?.Id != assemblyName.Name)
                throw new Exception($"The specified CalamariFlavour '{CalamariFlavour?.Id}' doesn't match that of the program exe '{assemblyName.Name}'");
        }

        public void SetVariables(TestVariableDictionary variables)
        {
            this.variables = variables;
        }
    }
}
