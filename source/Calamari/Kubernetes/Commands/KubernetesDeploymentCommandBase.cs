#if !NET40
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Calamari.Aws.Integration;
using Calamari.Commands;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Features.Behaviours;
using Calamari.Common.Features.ConfigurationTransforms;
using Calamari.Common.Features.ConfigurationVariables;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.StructuredVariables;
using Calamari.Common.Features.Substitutions;
using Calamari.Common.Plumbing.Deployment;
using Calamari.Common.Plumbing.Deployment.Journal;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Kubernetes.Conventions;
using Calamari.Kubernetes.Integration;

namespace Calamari.Kubernetes.Commands
{
    public abstract class KubernetesDeploymentCommandBase  : Command
    {
        public const string PackageDirectoryName = "package";
        private const string InlineYamlFileName = "customresource.yml";

        private readonly ILog log;
        private readonly IDeploymentJournalWriter deploymentJournalWriter;
        private readonly IVariables variables;
        private readonly ICalamariFileSystem fileSystem;
        private readonly IExtractPackage extractPackage;
        private readonly ISubstituteInFiles substituteInFiles;
        private readonly IStructuredConfigVariablesService structuredConfigVariablesService;
        private readonly Kubectl kubectl;
        private PathToPackage pathToPackage;

        protected KubernetesDeploymentCommandBase(
            ILog log,
            IDeploymentJournalWriter deploymentJournalWriter,
            IVariables variables,
            ICalamariFileSystem fileSystem,
            IExtractPackage extractPackage,
            ISubstituteInFiles substituteInFiles,
            IStructuredConfigVariablesService structuredConfigVariablesService,
            Kubectl kubectl)
        {
            this.log = log;
            this.deploymentJournalWriter = deploymentJournalWriter;
            this.variables = variables;
            this.fileSystem = fileSystem;
            this.extractPackage = extractPackage;
            this.substituteInFiles = substituteInFiles;
            this.structuredConfigVariablesService = structuredConfigVariablesService;
            this.kubectl = kubectl;

            Options.Add("package=", "Path to the NuGet package to install.",
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
                    if (pathToPackage != null)
                    {
                        extractPackage.ExtractToCustomDirectory(pathToPackage, packageDirectory);
                    }
                    else
                    {
                        //If using the inline yaml, copy it to the staging directory.
                        var inlineFile = Path.Combine(workingDirectory, InlineYamlFileName);
                        if (fileSystem.FileExists(inlineFile))
                        {
                            fileSystem.MoveFile(inlineFile, Path.Combine(packageDirectory, InlineYamlFileName));
                        }
                    }
                    d.StagingDirectory = stagingDirectory;
                    d.CurrentDirectoryProvider = DeploymentWorkingDirectory.StagingDirectory;
                    kubectl.SetWorkingDirectory(stagingDirectory);
                    kubectl.SetEnvironmentVariables(d.EnvironmentVariables);
                }),
                new SubstituteInFilesConvention(new SubstituteInFilesBehaviour(substituteInFiles, PackageDirectoryName)),
                new ConfigurationTransformsConvention(new ConfigurationTransformsBehaviour(fileSystem, variables,
                    ConfigurationTransformer.FromVariables(variables, log),
                    new TransformFileLocator(fileSystem, log), log, PackageDirectoryName)),
                new ConfigurationVariablesConvention(new ConfigurationVariablesBehaviour(fileSystem, variables,
                    new ConfigurationVariablesReplacer(variables, log), log, PackageDirectoryName)),
                new StructuredConfigurationVariablesConvention(
                    new StructuredConfigurationVariablesBehaviour(structuredConfigVariablesService, PackageDirectoryName))
            };

            if (variables.Get(Deployment.SpecialVariables.Account.AccountType) == "AmazonWebServicesAccount")
            {
                conventions.Add(new AwsAuthConvention(log, variables));
            }

            conventions.Add(new KubernetesAuthContextConvention(log, new CommandLineRunner(log, variables), kubectl));

            conventions.AddRange(CommandSpecificInstallConventions());

            var runningDeployment = new RunningDeployment(pathToPackage, variables);

            var conventionRunner = new ConventionProcessor(runningDeployment, conventions, log);
            try
            {
                conventionRunner.RunConventions(logExceptions: false);
                var result = ExecuteCommand(runningDeployment).GetAwaiter().GetResult();
                deploymentJournalWriter.AddJournalEntry(runningDeployment, result, pathToPackage);
                return result ? 0 : -1;
            }
            catch (Exception)
            {
                deploymentJournalWriter.AddJournalEntry(runningDeployment, false, pathToPackage);
                throw;
            }
        }
    }
}
#endif