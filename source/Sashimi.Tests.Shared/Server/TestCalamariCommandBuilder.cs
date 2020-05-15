using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Calamari;
using Calamari.Integration.FileSystem;
using Calamari.Tests.Shared.Helpers;
using Calamari.Util;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.ActionHandlers;
using Sashimi.Server.Contracts.Calamari;
using Sashimi.Server.Contracts.CommandBuilders;
using Sashimi.Server.Contracts.DeploymentTools;
using Sashimi.Tests.Shared.LogParser;

namespace Sashimi.Tests.Shared.Server
{
    public class TestCalamariCommandBuilder<TCalamariProgram> : ICalamariCommandBuilder where TCalamariProgram : CalamariFlavourProgram
    {
        TestVariableDictionary variables = new TestVariableDictionary();
        bool withStagedPackageArgument = false;
        
        public CalamariFlavour? CalamariFlavour { get; set; }
        public string? CalamariCommand { get; set; }
        public List<(string? filename, Stream contents)> Files = new List<(string?, Stream)>();
        public List<(string name, string? value)> Arguments = new List<(string, string?)>();
        public List<string> Extensions = new List<string>();

        public IList<IDeploymentTool> Tools { get;} = new List<IDeploymentTool>();

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
            List<string> GetArgs(string workingPath)
            {
                var args = new List<string> {CalamariCommand!};

                args.AddRange(
                    Arguments
                        .Select(a => $"--{a.name}{(a.value == null ? "" : $"={a.value}")}")
                );
                args.AddRange(Extensions.Select(e => $"--extension={e}"));

                var varPath = Path.Combine(workingPath, "variables.json");

                variables.Save(varPath);
                args.Add($"--variables={varPath}");

                //TODO: Deal with sensitive variables
                // variableArgs += $" -sensitiveVariables=\"{sshBashPaths.BuildPath(sshBashPaths.WorkingDirectory, "variables.secret")}\" -sensitiveVariablesPassword=$1";
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
                    var folderName = variables.GetRaw(KnownVariables.Action.Packages.PackageId);

                    Copy(folderName, workingPath);
                }
            }

            IActionHandlerResult ExecuteActionHandler(List<string> args)
            {
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
                })!;

                var methodInfo =
                    typeof(CalamariFlavourProgram).GetMethod("Run", BindingFlags.Instance | BindingFlags.NonPublic);
                if (methodInfo == null)
                {
                    throw new Exception("CalamariFlavourProgram.Run method was not found.");
                }

                var exitCode = (int) methodInfo.Invoke(instance, new object?[] {args.ToArray()})!;
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
                    new Dictionary<string, OutputVariable>(outputFilter.OutputVariables), outputFilter.Actions,
                    outputFilter.ServiceMessages, outputFilter.ResultMessage, outputFilter.Artifacts,
                    serverInMemoryLog.ToString());
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