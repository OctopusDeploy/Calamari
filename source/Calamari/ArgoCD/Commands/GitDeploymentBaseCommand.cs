using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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

namespace Calamari.ArgoCD.Commands
{
    public abstract class GitDeploymentBaseCommand : Command
    {
        public const string PackageDirectoryName = "package";

        readonly ILog log;
        readonly IDeploymentJournalWriter deploymentJournalWriter;
        readonly IVariables variables;
        readonly ICalamariFileSystem fileSystem;
        readonly IExtractPackage extractPackage;
        readonly ISubstituteInFiles substituteInFiles;
        readonly IStructuredConfigVariablesService structuredConfigVariablesService;
        PathToPackage pathToPackage;

        protected GitDeploymentBaseCommand(ILog log,
                                              IDeploymentJournalWriter deploymentJournalWriter,
                                              IVariables variables,
                                              ICalamariFileSystem fileSystem,
                                              IExtractPackage extractPackage,
                                              ISubstituteInFiles substituteInFiles,
                                              IStructuredConfigVariablesService structuredConfigVariablesService)
        {
            this.log = log;
            this.deploymentJournalWriter = deploymentJournalWriter;
            this.variables = variables;
            this.fileSystem = fileSystem;
            this.extractPackage = extractPackage;
            this.substituteInFiles = substituteInFiles;
            this.structuredConfigVariablesService = structuredConfigVariablesService;

            Options.Add("package=",
                        "Path to the NuGet package to install.",
                        v => pathToPackage = new PathToPackage(Path.GetFullPath(v)));
        }

        protected virtual IEnumerable<IInstallConvention> CommandSpecificInstallConventions() =>
            Enumerable.Empty<IInstallConvention>();

        /// <remarks>
        /// This empty implementation uses Task.FromResult(new object()); instead of
        /// Task.CompletedTask because Task.CompletedTask was only added in .NET 4.6.1
        /// so it is not compatible with Calamari.
        /// </remarks>
        protected virtual async Task<bool> ExecuteCommand(RunningDeployment runningDeployment) =>
            await Task.FromResult(true);

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);
            var conventions = new List<IConvention>
            {
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
                                                               new StructuredConfigurationVariablesBehaviour(structuredConfigVariablesService, PackageDirectoryName))
            };
            conventions.AddRange(CommandSpecificInstallConventions());

            var runningDeployment = new RunningDeployment(pathToPackage, variables);

            var conventionRunner = new ConventionProcessor(runningDeployment, conventions, log);
            conventionRunner.RunConventions(logExceptions: false);
            var result = ExecuteCommand(runningDeployment).GetAwaiter().GetResult();
            return result ? 0 : -1;
        }
    }
}