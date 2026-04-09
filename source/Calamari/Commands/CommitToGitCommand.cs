using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.ArgoCD.Git.PullRequests;
using Calamari.Commands.Support;
using Calamari.CommitToGit;
using Calamari.Common.Features.ConfigurationTransforms;
using Calamari.Common.Features.ConfigurationVariables;
using Calamari.Common.Features.StructuredVariables;
using Calamari.Common.Features.Substitutions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Common.Commands;
using Calamari.Common.Features.Behaviours;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.Deployment;
using Calamari.Common.Plumbing.Deployment.Journal;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Util;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Deployment.Conventions.DependencyVariables;

namespace Calamari.Commands;

[Command(Name, Description = "Update Kubernetes manifests from a package for one or more Argo CD Applications, persisting them in a Git repository")]
public class CommitToGitCommand : Command
{
    public const string Name = "update-argo-cd-app-manifests";
    public const string TransformsDirectoryName = "transforms";
    public const string InputsDirectoryName = "inputs";
    
    string scriptFileArg;
    PathToPackage pathToPackage;
    string scriptParametersArg;
    readonly ILog log;
    readonly IDeploymentJournalWriter deploymentJournalWriter;
    readonly INonSensitiveSubstituteInFiles nonSensitiveSubstituteInFiles;
    readonly ISubstituteInFiles substituteInFiles;
    readonly IGitVendorPullRequestClientResolver gitVendorPullRequestClientResolver;
    readonly IStructuredConfigVariablesService structuredConfigVariablesService;
    readonly ICalamariFileSystem fileSystem;
    readonly IVariables variables;
    readonly ICommandLineRunner commandLineRunner;
    readonly IScriptEngine scriptEngine;
    readonly IExtractPackage extractPackage;

    public CommitToGitCommand(ILog log, INonSensitiveSubstituteInFiles nonSensitiveSubstituteInFiles, ISubstituteInFiles substituteInFiles, IGitVendorPullRequestClientResolver gitVendorPullRequestClientResolver,
                              IStructuredConfigVariablesService structuredConfigVariablesService,
                              ICalamariFileSystem fileSystem,
                              IVariables variables,
                              ICommandLineRunner commandLineRunner,
                              IScriptEngine scriptEngine,
                              IDeploymentJournalWriter deploymentJournalWriter,
                              IExtractPackage extractPackage)
    {
        Options.Add("package=", "Path to the package to extract that contains the script.", v => pathToPackage = new PathToPackage(Path.GetFullPath(v)));
        Options.Add("script=", $"Path to the script to execute. If --package is used, it can be a script inside the package.", v => scriptFileArg = v);
        Options.Add("scriptParameters=", $"Parameters to pass to the script.", v => scriptParametersArg = v);

        this.log = log;
        this.nonSensitiveSubstituteInFiles = nonSensitiveSubstituteInFiles;
        this.substituteInFiles = substituteInFiles;
        this.gitVendorPullRequestClientResolver = gitVendorPullRequestClientResolver;
        this.structuredConfigVariablesService = structuredConfigVariablesService;
        this.fileSystem = fileSystem;
        this.variables = variables;
        this.commandLineRunner = commandLineRunner;
        this.scriptEngine = scriptEngine;
        this.deploymentJournalWriter = deploymentJournalWriter;
        this.extractPackage = extractPackage;
    }

    public override int Execute(string[] commandLineArguments)
    {
        Options.Parse(commandLineArguments);
        string baseWorkingDirectory = "";
        string transformsDirectory = "";
        string inputsDirectory = "";
        
        
        var deployment = new RunningDeployment(pathToPackage, variables);
        WriteVariableScriptToFile(deployment);

        var conventions = new List<IConvention>
        {
            new DelegateInstallConvention(d => baseWorkingDirectory = d.CurrentDirectory),
        };
        
        var stageTransformScriptAndSubstitute = new List<IConvention>
        {
            new DelegateInstallConvention(d =>
                                          {
                                              transformsDirectory = Path.Combine(baseWorkingDirectory, TransformsDirectoryName);
                                              fileSystem.EnsureDirectoryExists(transformsDirectory);
                                              d.StagingDirectory = transformsDirectory;
                                              d.CurrentDirectoryProvider = DeploymentWorkingDirectory.StagingDirectory;
                                          }),
            //we only want to include files which are NOT explicitly referenced as dependencies (i.e. we have files which are to be copied into the repo (referenced in variable), and some which should just be used for script dependencies. 
            new SelectiveDependencyStagingConvention(pathToPackage, fileSystem, new CombinedPackageExtractor(log, fileSystem, variables, commandLineRunner), new PackageVariablesFactory(), new NegatingExtractionChecker(new ExplicitlyReferencedDependencies(new CommitToGitDependencyMetadataParser(fileSystem, log)))),
            new SubstituteInFilesConvention(new SubstituteInFilesBehaviour(substituteInFiles)),
            // Substitute in the script itself.
            new DelegateInstallConvention(d => substituteInFiles.Substitute(d.CurrentDirectory, ScriptFileTargetFactory(d).ToList())),
        };

        var stagePackagesToIncludeInRepository = new List<IConvention>
        {
            new DelegateInstallConvention(d =>
                                          {
                                              inputsDirectory = Path.Combine(baseWorkingDirectory, InputsDirectoryName);
                                              fileSystem.EnsureDirectoryExists(inputsDirectory);
                                              d.StagingDirectory = inputsDirectory;
                                              d.CurrentDirectoryProvider = DeploymentWorkingDirectory.StagingDirectory;
                                          }),
            new SelectiveDependencyStagingConvention(pathToPackage,
                                                     fileSystem,
                                                     new CombinedPackageExtractor(log, fileSystem, variables, commandLineRunner),
                                                     new PackageVariablesFactory(),
                                                     new ExplicitlyReferencedDependencies(new CommitToGitDependencyMetadataParser(fileSystem, log))),
            new SubstituteInFilesConvention(new NonSensitiveSubstituteInFilesBehaviour(nonSensitiveSubstituteInFiles)),
        };
        
        // Create a repository and copy the inputs into the repository
        var repositoryOperations = new List<IConvention>
        {

        };
        
        // Execute the transform script over the repository from its 'base directory'
        var transformRepository = new List<IConvention>
        {
            
            new ExecuteScriptConvention(scriptEngine, commandLineRunner, log)
        };

        var commitToRemote = new List<IConvention>
        {

        };
        
        
        conventions.AddRange(stageTransformScriptAndSubstitute);
        conventions.AddRange(stagePackagesToIncludeInRepository);
        conventions.AddRange(repositoryOperations);
        conventions.AddRange(transformRepository);
        conventions.AddRange(stagePackagesToIncludeInRepository);
        conventions.AddRange(commitToRemote);
        
        var conventionRunner = new ConventionProcessor(deployment, conventions, log);
        conventionRunner.RunConventions();
        var exitCode = variables.GetInt32(SpecialVariables.Action.Script.ExitCode);
        deploymentJournalWriter.AddJournalEntry(deployment, exitCode == 0, pathToPackage);
        return exitCode.Value;
    }
    
    void WriteVariableScriptToFile(RunningDeployment deployment)
    {
        if (!TryGetScriptFromVariables(out var scriptBody, out var relativeScriptFile, out var scriptSyntax) &&
            !WasProvided(variables.Get(ScriptVariables.ScriptFileName)))
        {
            throw new CommandException($"Could not determine script to run.  Please provide either a `{ScriptVariables.ScriptBody}` variable, " +
                                       $"or a `{ScriptVariables.ScriptFileName}` variable.");
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
        yield return Path.Combine(deployment.CurrentDirectory, scriptFile);
    }

    bool WasProvided(string value)
    {
        return !string.IsNullOrEmpty(value);
    }

}