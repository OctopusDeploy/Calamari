using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Calamari;
using Calamari.Common;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing;
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
using KnownVariables = Sashimi.Server.Contracts.KnownVariables;

namespace Sashimi.Tests.Shared.Server
{
    class TestCalamariCommandBuilder<TCalamariProgram> : ICalamariCommandBuilder where TCalamariProgram : CalamariFlavourProgram
    {
        const string CalamariBinariesLocationEnvironmentVariable = "CalamariBinaries_RelativePath";

        static class InProcOutProcOverride
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
        public List<string> Extensions = new List<string>();

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

        public ICalamariCommandBuilder WithArgument(string name, string value)
        {
            throw new NotImplementedException();
        }

        public ICalamariCommandBuilder WithExtension(string extension)
        {
            Extensions.Add(extension);
            return this;
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

        public ICalamariCommandBuilder WithVariable(string name, string value, bool isSensitive = false)
        {
            throw new NotImplementedException();
        }

        public ICalamariCommandBuilder WithVariable(string name, bool value, bool isSensitive = false)
            => throw new NotImplementedException();

        public IActionHandlerResult Execute()
        {
            using (var working = TemporaryDirectory.Create())
            {
                var workingPath = working.DirectoryPath;

                //HACK: set the working directory, we will need to modify Calamari to not depend on this
                var originalWorkingDirectory = Environment.CurrentDirectory;
                try
                {
                    Environment.CurrentDirectory = workingPath;

                    var args = GetArgs(workingPath);

                    CopyFilesToWorkingFolder(workingPath);

                    return ExecuteActionHandler(args);
                }
                finally
                {
                    Environment.CurrentDirectory = originalWorkingDirectory;
                }
            }
        }

        List<string> GetArgs(string workingPath)
        {
            var args = new List<string> {CalamariCommand};

            args.AddRange(
                Arguments
                    .Select(a => $"--{a.name}{(a.value == null ? "" : $"={a.value}")}")
            );
            args.AddRange(Extensions.Select(e => $"--extension={e}"));

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

            if (withStagedPackageArgument)
            {
                var packageId = variables.GetRaw(KnownVariables.Action.Packages.PackageId);
                if (File.Exists(packageId))
                {
                    var fileName = new FileInfo(packageId).Name;
                    File.Copy(packageId, Path.Combine(workingPath, fileName));
                }
                else
                {
                    Copy(packageId, workingPath);
                }
            }
        }

        IActionHandlerResult ExecuteActionHandler(List<string> args)
        {
            var inProcOutProcOverride = Environment.GetEnvironmentVariable(InProcOutProcOverride.EnvironmentVariable);
            if (!string.IsNullOrEmpty(inProcOutProcOverride))
            {
                if (InProcOutProcOverride.InProcValue.Equals(inProcOutProcOverride, StringComparison.OrdinalIgnoreCase))
                    return ExecuteActionHandlerInProc(args);

                if (InProcOutProcOverride.OutProcValue.Equals(inProcOutProcOverride, StringComparison.OrdinalIgnoreCase))
                    return ExecuteActionHandlerOutProc(args);

                throw new Exception($"'{InProcOutProcOverride.EnvironmentVariable}' environment variable must be '{InProcOutProcOverride.InProcValue}' or '{InProcOutProcOverride.OutProcValue}'");
            }

            if (TestEnvironment.IsCI)
                return ExecuteActionHandlerOutProc(args);
            else
                return ExecuteActionHandlerInProc(args);
        }

        IActionHandlerResult ExecuteActionHandlerInProc(List<string> args)
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
                typeof(CalamariFlavourProgram).GetMethod("Run", BindingFlags.Instance | BindingFlags.NonPublic);
            if (methodInfo == null)
            {
                throw new Exception("CalamariFlavourProgram.Run method was not found.");
            }

            var exitCode = (int) methodInfo.Invoke(instance, new object?[] {args.ToArray()});
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

        IActionHandlerResult ExecuteActionHandlerOutProc(List<string> args)
        {
            using (var variablesFile = new TemporaryFile(Path.GetTempFileName()))
            {
                variables.Save(variablesFile.FilePath);

                var calamariFullPath = GetOutProcCalamariExePath();
                Console.WriteLine("Running Calamari OutProc from: "+ calamariFullPath);

                var commandLine = CreateCommandLine(calamariFullPath);
                foreach (var argument in args)
                    commandLine = commandLine.Argument(argument);

                var calamariResult = Invoke(commandLine);

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
            var sashimiTestFolder = Path.GetDirectoryName(typeof(TCalamariProgram).Assembly.FullLocalPath());

            if (TestEnvironment.IsCI)
            {
                //This is where Teamcity puts the Calamari binaries
                var calamaribinariesFolder = Environment.GetEnvironmentVariable(CalamariBinariesLocationEnvironmentVariable);
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

        CalamariResult Invoke(CommandLine command, IVariables? variables = null)
        {
            var runner = new TestCommandLineRunner(ConsoleLog.Instance, variables ?? new CalamariVariables());
            var result = runner.Execute(command.Build());
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

        public ICalamariCommandBuilder WithIsolation(ExecutionIsolation executionIsolation)
            => throw new NotImplementedException();

        public ICalamariCommandBuilder WithIsolationTimeout(TimeSpan mutexTimeout)
            => throw new NotImplementedException();

        public string Describe()
            => throw new NotImplementedException();

        public void SetVariables(TestVariableDictionary variables)
        {
            this.variables = variables;
        }
    }
}
