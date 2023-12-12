#if NETSTANDARD
using NuGet.Packaging;
using NuGet.Versioning;
#else
#endif
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Calamari.Common;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages.NuGet;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing.Helpers;
using Calamari.Testing.LogParser;
using FluentAssertions;
using NuGet;
using NuGet.Packaging;
using NuGet.Versioning;
using Octopus.CoreUtilities;
using KnownVariables = Calamari.Common.Plumbing.Variables.KnownVariables;
using OSPlatform = System.Runtime.InteropServices.OSPlatform;

namespace Calamari.Testing
{
    public static class CommandTestBuilder
    {
        public static CommandTestBuilder<TCalamari> CreateAsync<TCalamari>(string command)
            where TCalamari : CalamariFlavourProgramAsync
        {
            return new CommandTestBuilder<TCalamari>(command);
        }

        public static CommandTestBuilder<TCalamari> CreateAsync<TCommand, TCalamari>()
            where TCalamari : CalamariFlavourProgramAsync
            where TCommand : PipelineCommand
        {
            return new CommandTestBuilder<TCalamari>(typeof(TCommand).GetCustomAttribute<CommandAttribute>().Name);
        }

        public static CommandTestBuilder<TCalamari> Create<TCalamari>(string command)
            where TCalamari : CalamariFlavourProgram
        {
            return new CommandTestBuilder<TCalamari>(command);
        }

        public static CommandTestBuilder<TCalamari> Create<TCommand, TCalamari>()
            where TCalamari : CalamariFlavourProgram
            where TCommand : ICommand
        {
            return new CommandTestBuilder<TCalamari>(typeof(TCommand).GetCustomAttribute<CommandAttribute>().Name);
        }

        public static CommandTestBuilderContext WithFilesToCopy(this CommandTestBuilderContext context, string path)
        {
            if (File.Exists(path))
            {
                context.Variables.Add(KnownVariables.OriginalPackageDirectoryPath, Path.GetDirectoryName(path));
            }
            else
            {
                context.Variables.Add(KnownVariables.OriginalPackageDirectoryPath, path);
            }

            context.Variables.Add("Octopus.Test.PackagePath", path);
            context.Variables.Add("Octopus.Action.Package.FeedId", "FeedId");

            return context;
        }

        public static CommandTestBuilderContext WithPackage(this CommandTestBuilderContext context, string packagePath, string packageId, string packageVersion)
        {
            context.Variables.Add(KnownVariables.OriginalPackageDirectoryPath, Path.GetDirectoryName(packagePath));
            context.Variables.Add(TentacleVariables.CurrentDeployment.PackageFilePath, packagePath);
            context.Variables.Add("Octopus.Action.Package.PackageId", packageId);
            context.Variables.Add("Octopus.Action.Package.PackageVersion", packageVersion);
            context.Variables.Add("Octopus.Action.Package.FeedId", "FeedId");

            return context;
        }

        public static CommandTestBuilderContext WithNewNugetPackage(this CommandTestBuilderContext context, string packageRootPath, string packageId, string packageVersion)
        {
            var pathToPackage = Path.Combine(packageRootPath, CreateNugetPackage(packageId, packageVersion, packageRootPath));

            return context.WithPackage(pathToPackage, packageId, packageVersion);
        }

        static string CreateNugetPackage(string packageId, string packageVersion, string filePath)
        {
            var metadata = new ManifestMetadata
            {
                Authors = new [] {"octopus@e2eTests"},
                Version = new NuGetVersion(packageVersion),
                Id = packageId,
                Description = nameof(CommandTestBuilder)
            };

            var packageFileName = $"{packageId}.{metadata.Version}.nupkg";

            var builder = new PackageBuilder();
            builder.PopulateFiles(filePath, new[] { new ManifestFile { Source = "**" } });
            builder.Populate(metadata);

            using var stream = File.Open(Path.Combine(filePath, packageFileName), FileMode.OpenOrCreate);
            builder.Save(stream);

            return packageFileName;
        }
    }

    public class CommandTestBuilder<TCalamariProgram>
    {
        readonly string command;
        readonly List<Action<CommandTestBuilderContext>> arrangeActions;
        Action<TestCalamariCommandResult>? assertAction;

        internal CommandTestBuilder(string command)
        {
            this.command = command;
            arrangeActions = new List<Action<CommandTestBuilderContext>>();
        }

        public CommandTestBuilder<TCalamariProgram> WithArrange(Action<CommandTestBuilderContext> arrange)
        {
            arrangeActions.Add(arrange);
            return this;
        }

        public CommandTestBuilder<TCalamariProgram> WithAssert(Action<TestCalamariCommandResult> assert)
        {
            assertAction = assert;
            return this;
        }

        public async Task<TestCalamariCommandResult> Execute(bool assertWasSuccess = true)
        {
            var context = new CommandTestBuilderContext();

            List<string> GetArgs(string workingPath)
            {
                var args = new List<string> {command};

                var varPath = Path.Combine(workingPath, "variables.json");

                context.Variables.Save(varPath);
                args.Add($"--variables={varPath}");

                return args;
            }
            
            List<string> InstallTools(string toolsPath)
            {
                var extractor = new NupkgExtractor(new InMemoryLog());

                var modulePaths = new List<string>();
                var addToPath = new List<string>();
                var platform = "win-x64";

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    platform = "linux-x64";
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    platform = "osx-x64";

                foreach (var tool in context.Tools)
                {
                    var toolPath = Path.Combine(toolsPath, tool.Id);
                    modulePaths.AddRange(tool.GetCompatiblePackage(platform)
                                             .SelectValueOr(package => package.BootstrapperModulePaths, Enumerable.Empty<string>())
                                             .Select(s => Path.Combine(toolPath, s)));

                    var toolPackagePath = Path.Combine(Path.GetDirectoryName(AssemblyExtensions.FullLocalPath(Assembly.GetExecutingAssembly())) ?? string.Empty, $"{tool.Id}.nupkg");
                    if (!File.Exists(toolPackagePath))
                        throw new Exception($"{tool.Id}.nupkg missing.");

                    extractor.Extract(toolPackagePath, toolPath);
                    var fullPathToTool = tool.SubFolder.None()
                        ? toolPath
                        : Path.Combine(toolPath, tool.SubFolder.Value);
                    if (tool.ToolPathVariableToSet.Some())
                        context.Variables[tool.ToolPathVariableToSet.Value] = fullPathToTool
                                                                      .Replace("$HOME", "#{env:HOME}")
                                                                      .Replace("$TentacleHome", "#{env:TentacleHome}");

                    if (tool.AddToPath)
                        addToPath.Add(fullPathToTool);
                }

                var modules = string.Join(";", modulePaths);
                context.Variables["Octopus.Calamari.Bootstrapper.ModulePaths"] = modules;

                return addToPath;
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

            void CopyFilesToWorkingFolder(string workingPath)
            {
                foreach (var (filename, contents) in context.Files)
                {
                    using var fileStream = File.Create(Path.Combine(workingPath, filename!));
                    contents.Seek(0, SeekOrigin.Begin);
                    contents.CopyTo(fileStream);
                }

                if (!context.withStagedPackageArgument)
                {
                    var packageId = context.Variables.GetRaw("Octopus.Test.PackagePath");
                    if (File.Exists(packageId))
                    {
                        var fileName = new FileInfo(packageId).Name;
                        File.Copy(packageId, Path.Combine(workingPath, fileName));
                    }
                    else if (Directory.Exists(packageId))
                    {
                        Copy(packageId, workingPath);
                    }
                }
            }

            async Task<TestCalamariCommandResult> ExecuteActionHandler(List<string> args, string workingFolder, List<string> paths)
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
                                                            return await (Task<int>)methodInfo.Invoke(instance, new object?[] { args.ToArray() })!;

                                                        return (int)methodInfo.Invoke(instance, new object?[] { args.ToArray() })!;
                                                    });
                
                var serverInMemoryLog = new CalamariInMemoryTaskLog();
                var outputFilter = new ScriptOutputFilter(serverInMemoryLog);
                foreach (var text in inMemoryLog.StandardError)
                {
                    outputFilter.Write(ProcessOutputSource.StdErr, text);
                }

                foreach (var text in inMemoryLog.StandardOut)
                {
                    outputFilter.Write(ProcessOutputSource.StdOut, text);
                }

                return new TestCalamariCommandResult(exitCode,
                    outputFilter.TestOutputVariables, outputFilter.Actions,
                    outputFilter.ServiceMessages, outputFilter.ResultMessage, outputFilter.Artifacts,
                    serverInMemoryLog.ToString(), workingFolder);
            }

            foreach (var arrangeAction in arrangeActions)
            {
                arrangeAction.Invoke(context);
            }

            TestCalamariCommandResult result;

            using (var working = TemporaryDirectory.Create())
            {
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

                    result = await ExecuteActionHandler(args, workingPath, paths);

                    if (assertWasSuccess)
                    {
                        result.WasSuccessful.Should().BeTrue($"{command} execute result was unsuccessful");
                    }
                    assertAction?.Invoke(result);
                }
                finally
                {
                    Environment.CurrentDirectory = originalWorkingDirectory;
                }
            }

            return result;
        }
        
        Task<int> ExecuteWrapped(IReadOnlyCollection<string> paths, Func<Task<int>> func)
        {
            if (paths.Count > 0)
            {
                var originalPath = Environment.GetEnvironmentVariable("PATH");
                try
                {
                    Environment.SetEnvironmentVariable("PATH", $"{originalPath};{string.Join(";", paths)}", EnvironmentVariableTarget.Process);

                    return func();
                }
                finally
                {
                    Environment.SetEnvironmentVariable("PATH", originalPath, EnvironmentVariableTarget.Process);
                }
            }

            return func();
        }
    }
}