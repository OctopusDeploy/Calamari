using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.ArgoCD.Conventions.UpdateImageTag;
using Calamari.ArgoCD.Git;
using Calamari.ArgoCD.Git.PullRequests;
using Calamari.Commands.Support;
using Calamari.CommitToGit;
using Calamari.Common.Features.Substitutions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.Deployment;
using Calamari.Common.Plumbing.Deployment.Journal;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Deployment.Conventions.DependencyVariables;
using Calamari.Integration.Time;

namespace Calamari.Commands;

[Command(Name, Description = "Update a Git repository with selected package content, then transform with optional script")]
public class CommitToGitCommand : Command
{
    public const string Name = "commit-to-git";
    
    string scriptFileArg;
    PathToPackage pathToPackage;
    string scriptParametersArg;
    string customPropertiesFile;
    string customPropertiesPassword;
    readonly ILog log;
    readonly IDeploymentJournalWriter deploymentJournalWriter;
    readonly INonSensitiveSubstituteInFiles nonSensitiveSubstituteInFiles;
    readonly ISubstituteInFiles substituteInFiles;
    readonly IGitVendorPullRequestClientResolver gitVendorPullRequestClientResolver;
    readonly ICalamariFileSystem fileSystem;
    readonly IVariables variables;
    readonly ICommandLineRunner commandLineRunner;
    readonly IScriptEngine scriptEngine;
    readonly CommitToGitConfigFactory configFactory;

    public CommitToGitCommand(ILog log, INonSensitiveSubstituteInFiles nonSensitiveSubstituteInFiles, ISubstituteInFiles substituteInFiles, IGitVendorPullRequestClientResolver gitVendorPullRequestClientResolver,
                              ICalamariFileSystem fileSystem,
                              IVariables variables,
                              ICommandLineRunner commandLineRunner,
                              IScriptEngine scriptEngine,
                              IDeploymentJournalWriter deploymentJournalWriter,
                              CommitToGitConfigFactory configFactory)
    {
        Options.Add("package=", "Path to the package to extract that contains the script.", v => pathToPackage = new PathToPackage(Path.GetFullPath(v)));
        Options.Add("script=", $"Path to the script to execute. If --package is used, it can be a script inside the package.", v => scriptFileArg = v);
        Options.Add("scriptParameters=", $"Parameters to pass to the script.", v => scriptParametersArg = v);
        Options.Add("customPropertiesFile=", "Path to an encrypted JSON file containing the git credential.", v => customPropertiesFile = Path.GetFullPath(v));
        Options.Add("customPropertiesPassword=", "Password to decrypt the custom properties file.", v => customPropertiesPassword = v);

        this.log = log;
        this.nonSensitiveSubstituteInFiles = nonSensitiveSubstituteInFiles;
        this.substituteInFiles = substituteInFiles;
        this.gitVendorPullRequestClientResolver = gitVendorPullRequestClientResolver;
        this.fileSystem = fileSystem;
        this.variables = variables;
        this.commandLineRunner = commandLineRunner;
        this.scriptEngine = scriptEngine;
        this.deploymentJournalWriter = deploymentJournalWriter;
        this.configFactory = configFactory;
    }

    public override int Execute(string[] commandLineArguments)
    {
        Options.Parse(commandLineArguments);
        ApplyScriptParametersOverride();

        if (!WasProvided(customPropertiesFile))
            throw new CommandException("Required option --customPropertiesFile was not provided.");
        if (!WasProvided(customPropertiesPassword))
            throw new CommandException("Required option --customPropertiesPassword was not provided.");
        if (!fileSystem.FileExists(customPropertiesFile))
            throw new CommandException($"Custom properties file '{customPropertiesFile}' does not exist.");

        var customPropertiesLoader = new CustomPropertiesLoader(fileSystem, customPropertiesFile, customPropertiesPassword);

        var deployment = new RunningDeployment(pathToPackage, variables);
        var repositoryConfig = configFactory.CreateRepositoryConfig(deployment, customPropertiesLoader);
        var repositoryFactory = new RepositoryFactory(log, fileSystem, deployment.CurrentDirectory, gitVendorPullRequestClientResolver, new SystemClock());
        using var clonedRepository = repositoryFactory.CloneRepository(UniqueRepoNameGenerator.Generate(), repositoryConfig.GitConnection);
        deployment.Variables.Set("Octopus.Calamari.Git.RepositoryPath", clonedRepository.WorkingDirectory);
        var metadataParser = new CommitToGitDependencyMetadataParser(fileSystem);
        WriteVariableScriptToFile(deployment);

        var conventions = new List<IConvention>();
        conventions.AddRange(BuildExtractAllPackagesConventions());
        conventions.AddRange(BuildSubstituteScriptPackagesConventions(metadataParser));
        conventions.AddRange(BuildSubstituteAndCopyInputFilesConventions(metadataParser, repositoryConfig, clonedRepository));
        conventions.AddRange(BuildTransformRepositoryConventions());
        conventions.AddRange(BuildCommitToRemoteConventions(repositoryConfig, clonedRepository));

        new ConventionProcessor(deployment, conventions, log).RunConventions();

        var exitCode = variables.GetInt32(SpecialVariables.Action.Script.ExitCode) ?? 0;
        deploymentJournalWriter.AddJournalEntry(deployment, exitCode == 0, pathToPackage);
        return exitCode;
    }

    void ApplyScriptParametersOverride()
    {
        if (!WasProvided(scriptParametersArg))
            return;

        if (WasProvided(variables.Get(SpecialVariables.Action.Script.ScriptParameters)))
        {
            log.Warn($"The `--scriptParameters` parameter and `{SpecialVariables.Action.Script.ScriptParameters}` variable are both set.\r\n" +
                     $"Please provide just the `{SpecialVariables.Action.Script.ScriptParameters}` variable instead.");
        }
        else
        {
            variables.Set(SpecialVariables.Action.Script.ScriptParameters, scriptParametersArg);
        }
    }

    IEnumerable<IConvention> BuildExtractAllPackagesConventions()
    {
        //we only want to include files which are NOT explicitly referenced as dependencies (i.e. we have files which are to be copied into the repo (referenced in variable), and some which should just be used for script dependencies.
        return new IConvention[]
        {
            new StageDependenciesConvention(pathToPackage, fileSystem, new CombinedPackageExtractor(log, fileSystem, variables, commandLineRunner), new PackageVariablesFactory(), true),
            new StageDependenciesConvention(null, fileSystem, new CombinedPackageExtractor(log, fileSystem, variables, commandLineRunner), new GitDependencyVariablesFactory(), true),  // don't re-extract script.
            new DelegateInstallConvention(d => substituteInFiles.Substitute(d.CurrentDirectory, ScriptFileTargetFactory(d).ToList())),
        };
    }

    IEnumerable<IConvention> BuildSubstituteScriptPackagesConventions(CommitToGitDependencyMetadataParser metadataParser)
    {
        return
        [
            new DelegateInstallConvention(d =>
                                          {
                                              var packageVariablesFactory = new PackageVariablesFactory();
                                              var allPackageNames = packageVariablesFactory.GetDependencyVariables(d.Variables).GetIndexes().ToList();
                                              var inputPackageNames = metadataParser.ReferencedDependencyNames(d).ToList();

                                              var scriptPackageNames = allPackageNames.Except(inputPackageNames);
                                              foreach (var packageName in scriptPackageNames)
                                              {
                                                  var packagePath = Path.Combine(d.CurrentDirectory, packageName);
                                                  substituteInFiles.Substitute(packagePath, fileSystem.EnumerateFilesRecursively(packagePath).ToList());
                                              }
                                          })
        ];
    }

    IEnumerable<IConvention> BuildSubstituteAndCopyInputFilesConventions(
        CommitToGitDependencyMetadataParser metadataParser,
        CommitToGitRepositorySettings repositoryConfig,
        RepositoryWrapper clonedRepository)
    {
        return
        [
            new DelegateInstallConvention(d =>
                                          {
                                              var destinationPath = repositoryConfig!.DestinationPath ?? string.Empty;
                                              var destBase = Path.Combine(clonedRepository.WorkingDirectory, destinationPath);

                                              foreach (var package in metadataParser.GetPackageDependenciesForCopying(d))
                                              {
                                                  CopyDependencyToRepository(d, clonedRepository, destBase, new CopyDependencySpec(
                                                      "Package",
                                                      package.PackageName,
                                                      package.DestinationSubFolder,
                                                      dir => fileSystem.EnumerateFilesWithGlob(dir, package.InputFilePaths)));
                                              }

                                              foreach (var gitDep in metadataParser.GetGitRepositoryDependenciesForCopying(d))
                                              {
                                                  //no input-globs are required as only the relevant files were transmitted to Calamari
                                                  var copiedTo = CopyDependencyToRepository(d, clonedRepository, destBase, new CopyDependencySpec(
                                                      "Git dependency",
                                                      gitDep.GitDependencyName,
                                                      gitDep.DestinationSubFolder,
                                                      dir => fileSystem.EnumerateFilesRecursively(dir)));
                                                  if (copiedTo != null)
                                                      log.Verbose($"Copied files for git dependency '{gitDep.GitDependencyName}' to {copiedTo}");
                                              }
                                          })
        ];
    }

    string CopyDependencyToRepository(RunningDeployment deployment, RepositoryWrapper clonedRepository, string destBase, CopyDependencySpec spec)
    {
        var sanitizedName = fileSystem.RemoveInvalidFileNameChars(spec.Name);
        var sourceDir = Path.Combine(deployment.CurrentDirectory, sanitizedName);
        if (!fileSystem.DirectoryExists(sourceDir))
        {
            log.Verbose($"{spec.Kind} source directory '{sourceDir}' not found, skipping");
            return null;
        }

        var filesToTarget = spec.EnumerateFiles(sourceDir).ToList();
        nonSensitiveSubstituteInFiles.Substitute(deployment.CurrentDirectory, filesToTarget);

        var depDestBase = Path.Combine(destBase, spec.DestinationSubFolder ?? string.Empty);
        EnsurePathInsideWorkingDirectory(clonedRepository.WorkingDirectory, depDestBase, $"DestinationSubFolder for {spec.Kind.ToLowerInvariant()} '{spec.Name}'");
        foreach (var sourceFile in filesToTarget)
        {
            var relativePath = Path.GetRelativePath(sourceDir, sourceFile);
            var destFile = Path.Combine(depDestBase, relativePath);
            fileSystem.EnsureDirectoryExists(Path.GetDirectoryName(destFile)!);
            fileSystem.CopyFile(sourceFile, destFile);
        }
        return depDestBase;
    }

    record CopyDependencySpec(string Kind, string Name, string DestinationSubFolder, Func<string, IEnumerable<string>> EnumerateFiles);

    // Execute the transform script over the repository from its 'base directory'
    IEnumerable<IConvention> BuildTransformRepositoryConventions()
    {
        return
        [
            new DelegateInstallConvention(d =>
                                          {
                                              var scriptFileName = d.Variables.Get(ScriptVariables.ScriptFileName);
                                              if (!WasProvided(scriptFileName))
                                              {
                                                  log.Verbose("No transform script configured, skipping script execution.");
                                                  log.SetOutputVariable(SpecialVariables.Action.Script.ExitCode, "0", d.Variables);
                                                  return;
                                              }
                                              new ExecuteScriptConvention(scriptEngine, commandLineRunner, log).Install(d);
                                          })
        ];
    }

    IEnumerable<IConvention> BuildCommitToRemoteConventions(CommitToGitRepositorySettings repositoryConfig, RepositoryWrapper clonedRepository)
    {
        return new IConvention[]
        {
            new DelegateInstallConvention(d =>
                                          {
                                              var commitParams = repositoryConfig!.CommitParameters;
                                              var updater = new RepositoryUpdater(commitParams, log, new UserDefinedCommitMessageGenerator(commitParams.Description));
                                              
                                              var pushResult = updater.PushToRemote(clonedRepository, repositoryConfig.GitConnection.GitReference, FileUpdateResult.EmptyFileUpdateResult);
                                              new CommitToGitOutputVariablesWriter(log).WritePushResultOutput(pushResult);
                                          })
        };
    }
    
    void WriteVariableScriptToFile(RunningDeployment deployment)
    {
        if (!TryGetScriptFromVariables(out var scriptBody, out var relativeScriptFile, out var scriptSyntax) &&
            !WasProvided(variables.Get(ScriptVariables.ScriptFileName)))
        {
            log.Info($"No inline transformation script has been defined");
            return;
        }

        if (WasProvided(scriptBody))
        {
            var scriptFile = Path.Combine(deployment.CurrentDirectory, relativeScriptFile);

            //Set the name of the script we are about to create to the variables collection for replacement later on
            variables.Set(ScriptVariables.ScriptFileName, relativeScriptFile);

            // If the script body was supplied via a variable, then we write it out to a file.
            // This will be deleted with the working directory.
            // Bash files need SheBang as first few characters. This does not play well with BOM characters
            var scriptBytes = scriptSyntax == ScriptSyntax.Bash
                ? scriptBody.EncodeInUtf8NoBom()
                : scriptBody.EncodeInUtf8Bom();
            File.WriteAllBytes(scriptFile, scriptBytes);
        }
    }
    
    bool TryGetScriptFromVariables(out string scriptBody, out string scriptFileName, out ScriptSyntax syntax)
    {
        scriptBody = variables.GetRaw(ScriptVariables.ScriptBody);
        if (WasProvided(scriptBody))
        {
            var scriptSyntax = variables.Get(ScriptVariables.Syntax);
            if (scriptSyntax == null)
            {
                syntax = scriptEngine.GetSupportedTypes().FirstOrDefault();
                log.Warn($"No script syntax provided. Defaulting to first known supported type {syntax}");
            }
            else if (!Enum.TryParse(scriptSyntax, out syntax))
            {
                throw new CommandException($"Unknown script syntax `{scriptSyntax}` provided");
            }

            scriptFileName = "Script." + syntax.FileExtension();
            return true;
        }

        // Try get any supported script body variable
        foreach (var supportedSyntax in scriptEngine.GetSupportedTypes())
        {
            scriptBody = variables.GetRaw(SpecialVariables.Action.Script.ScriptBodyBySyntax(supportedSyntax));
            if (scriptBody == null)
            {
                continue;
            }

            scriptFileName = "Script." + supportedSyntax.FileExtension();
            syntax = supportedSyntax;
            return true;
        }

        scriptBody = null;
        syntax = 0;
        scriptFileName = null;
        return false;
    }
    
    IEnumerable<string> ScriptFileTargetFactory(RunningDeployment deployment)
    {
        // We should not perform variable-replacement if a file arg is passed in since this deprecated property
        // should only be coming through if something isn't using the variable-dictionary and hence will
        // have already been replaced on the server
        if (WasProvided(scriptFileArg) && !WasProvided(pathToPackage))
        {
            yield break;
        }

        var scriptFile = deployment.Variables.Get(ScriptVariables.ScriptFileName);
        if (!WasProvided(scriptFile))
            yield break;

        yield return Path.Combine(deployment.CurrentDirectory, scriptFile);
    }

    bool WasProvided(string value)
    {
        return !string.IsNullOrEmpty(value);
    }

    static void EnsurePathInsideWorkingDirectory(string workingDirectory, string candidatePath, string description)
    {
        var workingDirFull = Path.GetFullPath(workingDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var candidateFull = Path.GetFullPath(candidatePath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!candidateFull.StartsWith(workingDirFull, StringComparison.Ordinal))
        {
            throw new CommandException($"{description} ('{candidatePath}') resolves outside the cloned repository.");
        }
    }
}
