using System;
using System.Collections.Generic;
using System.IO;
using Calamari.ArgoCD.Conventions;
using Calamari.Commands;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Features.Behaviours;
using Calamari.Common.Features.ConfigurationTransforms;
using Calamari.Common.Features.ConfigurationVariables;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.StructuredVariables;
using Calamari.Common.Features.Substitutions;
using Calamari.Common.Plumbing.Deployment;
using Calamari.Common.Plumbing.Deployment.Journal;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using LibGit2Sharp;

namespace Calamari.ArgoCD.Commands
{
    [Command(Name, Description = "Write populated templates to git repository")]
    public class UpdateGitRepoFromTemplates : Command
    {
        public const string PackageDirectoryName = "package";
        public const string GitRepositoryDirectoryName = "git";

        public const string Name = "update-git-repo-from-templates";

        readonly ILog log;
        readonly IVariables variables;
        readonly ICalamariFileSystem fileSystem;
        readonly IExtractPackage extractPackage;
        readonly ISubstituteInFiles substituteInFiles;
        readonly IStructuredConfigVariablesService structuredConfigVariablesService;
        PathToPackage pathToPackage;

        public UpdateGitRepoFromTemplates(
            ILog log,
            IDeploymentJournalWriter deploymentJournalWriter,
            IVariables variables,
            ICalamariFileSystem fileSystem,
            IExtractPackage extractPackage,
            ISubstituteInFiles substituteInFiles,
            IStructuredConfigVariablesService structuredConfigVariablesService)
        {
            this.log = log;
            this.variables = variables;
            this.fileSystem = fileSystem;
            this.extractPackage = extractPackage;
            this.substituteInFiles = substituteInFiles;
            this.structuredConfigVariablesService = structuredConfigVariablesService;

            Options.Add("package=",
                        "Path to the NuGet package to install.",
                        v => pathToPackage = new PathToPackage(Path.GetFullPath(v)));
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);
            var repositories = new List<Repository>();

            var conventions = new List<IConvention>()
            {
                new DelegateInstallConvention(d =>
                                              {
                                                  var workingDirectory = d.CurrentDirectory;
                                                  var gitRepositoryRoot = Path.Combine(workingDirectory, GitRepositoryDirectoryName);
                                                  var convention = new GitCloneConvention(gitRepositoryRoot);
                                                  convention.Install(d);
                                                  repositories.AddRange(convention.Repositories); //this needs to make multiple repos
                                              }),
                new DelegateInstallConvention(d =>
                                              {
                                                  var workingDirectory = d.CurrentDirectory;
                                                  var stagingDirectory = Path.Combine(workingDirectory, ExtractPackage.StagingDirectoryName);
                                                  var packageDirectory = Path.Combine(stagingDirectory, PackageDirectoryName);
                                                  fileSystem.EnsureDirectoryExists(packageDirectory);
                                                  extractPackage.ExtractToCustomDirectory(pathToPackage, packageDirectory);

                                                  d.StagingDirectory = stagingDirectory;
                                                  d.CurrentDirectoryProvider = DeploymentWorkingDirectory.StagingDirectory;
                                              }),
                new SubstituteInFilesConvention(new SubstituteInFilesBehaviour(substituteInFiles, PackageDirectoryName)),
                new ConfigurationTransformsConvention(new ConfigurationTransformsBehaviour(fileSystem,
                                                                                           variables,
                                                                                           ConfigurationTransformer.FromVariables(variables, log),
                                                                                           new TransformFileLocator(fileSystem, log),
                                                                                           log,
                                                                                           PackageDirectoryName)),
                new ConfigurationVariablesConvention(new ConfigurationVariablesBehaviour(fileSystem,
                                                                                         variables,
                                                                                         new ConfigurationVariablesReplacer(variables, log),
                                                                                         log,
                                                                                         PackageDirectoryName)),
                new StructuredConfigurationVariablesConvention(
                                                               new StructuredConfigurationVariablesBehaviour(structuredConfigVariablesService, PackageDirectoryName)),
                new UpdateRepositoryConvention(repositories, fileSystem),
                new GitPushConvention(repositories),
            };

            var runningDeployment = new RunningDeployment(pathToPackage, variables);

            var conventionRunner = new ConventionProcessor(runningDeployment, conventions, log);
            conventionRunner.RunConventions(logExceptions: false);

            //need to work out if the conventions somehow fails
            return 1;
        }
    }
}