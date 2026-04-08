using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.ArgoCD.Git.PullRequests;
using Calamari.Commands.Support;
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
using Calamari.Common.Plumbing.Deployment;
using Calamari.Common.Plumbing.Deployment.Journal;
using Calamari.Deployment.Conventions;
using Calamari.Deployment.Conventions.DependencyVariables;
using Calamari.Integration.Time;

namespace Calamari.Commands;

[Command(Name, Description = "Update Kubernetes manifests from a package for one or more Argo CD Applications, persisting them in a Git repository")]
public class CommitToGitCommand : Command
{
    public const string Name = "update-argo-cd-app-manifests";
    public const string TransformsDirectoryName = "transforms";
    
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
        var clock = new SystemClock();

        var conventions = new List<IConvention>
        {
            new DelegateInstallConvention(d =>
                                          {
                                              var workingDirectory = d.CurrentDirectory;
                                              var packageDirectory = Path.Combine(workingDirectory, TransformsDirectoryName);
                                              fileSystem.EnsureDirectoryExists(packageDirectory);
                                              extractPackage.ExtractToCustomDirectory(pathToPackage, packageDirectory);

                                              d.StagingDirectory = workingDirectory;
                                              d.CurrentDirectoryProvider = DeploymentWorkingDirectory.StagingDirectory;
                                          }),
        };


        var configurationTransformer = ConfigurationTransformer.FromVariables(variables, log);
        var transformFileLocator = new TransformFileLocator(fileSystem, log);
        var replacer = new ConfigurationVariablesReplacer(variables, log);

        var deployment = new RunningDeployment(pathToPackage, variables);

        var conventions = new List<IConvention>
        {
            new StageDependenciesConvention(pathToPackage, fileSystem, new CombinedPackageExtractor(log, fileSystem, variables, commandLineRunner), new PackageVariablesFactory())
        };

        conventions.AddRange(new IConvention[]
        {
            // Substitute the script source file
            new DelegateInstallConvention(d => substituteInFiles.Substitute(d.CurrentDirectory, ScriptFileTargetFactory(d).ToList())),
            // Substitute any user-specified files
            new SubstituteInFilesConvention(new SubstituteInFilesBehaviour(substituteInFiles)),
            new ConfigurationTransformsConvention(new ConfigurationTransformsBehaviour(fileSystem,
                                                                                       variables,
                                                                                       configurationTransformer,
                                                                                       transformFileLocator,
                                                                                       log)),
            new ConfigurationVariablesConvention(new ConfigurationVariablesBehaviour(fileSystem, variables, replacer, log)),
            new StructuredConfigurationVariablesConvention(new StructuredConfigurationVariablesBehaviour(structuredConfigVariablesService)),
            new ExecuteScriptConvention(scriptEngine, commandLineRunner, log)
        });
    }
    
    IEnumerable<string> ScriptFileTargetFactory(RunningDeployment deployment)
    {
        // We should not perform variable-replacement if a file arg is passed in since this deprecated property
        // should only be coming through if something isn't using the variable-dictionary and hence will
        // have already been replaced on the server
        if (WasProvided(scriptFileArg) && !WasProvided(packageFile))
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