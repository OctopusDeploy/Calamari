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

        protected virtual async Task ExecuteCommand(RunningDeployment runningDeployment) =>
            await Task.CompletedTask;

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);
            var conventions = new List<IConvention>
            {
                new DelegateInstallConvention(d =>
                {
                    if (pathToPackage != null)
                    {
                        extractPackage.ExtractToStagingDirectory(pathToPackage);
                    }
                    else
                    {
                        //If using the inline yaml, copy it to the staging directory.
                        var inlineFile = Path.Combine(d.CurrentDirectory, "customresource.yml");
                        var stagingDirectory = Path.Combine(d.CurrentDirectory, "staging");
                        fileSystem.EnsureDirectoryExists(stagingDirectory);
                        if (fileSystem.FileExists(inlineFile))
                        {
                            fileSystem.MoveFile(inlineFile, Path.Combine(stagingDirectory, "customresource.yml"));
                        }

                        d.StagingDirectory = stagingDirectory;
                        d.CurrentDirectoryProvider = DeploymentWorkingDirectory.StagingDirectory;
                    }

                    kubectl.SetWorkingDirectory(d.CurrentDirectory);
                    kubectl.SetEnvironmentVariables(d.EnvironmentVariables);
                }),
                new SubstituteInFilesConvention(new SubstituteInFilesBehaviour(substituteInFiles)),
                new ConfigurationTransformsConvention(new ConfigurationTransformsBehaviour(fileSystem, variables,
                    ConfigurationTransformer.FromVariables(variables, log),
                    new TransformFileLocator(fileSystem, log), log)),
                new ConfigurationVariablesConvention(new ConfigurationVariablesBehaviour(fileSystem, variables,
                    new ConfigurationVariablesReplacer(variables, log), log)),
                new StructuredConfigurationVariablesConvention(
                    new StructuredConfigurationVariablesBehaviour(structuredConfigVariablesService))
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
                ExecuteCommand(runningDeployment).GetAwaiter().GetResult();
                deploymentJournalWriter.AddJournalEntry(runningDeployment, true, pathToPackage);
            }
            catch (Exception e)
            {
                deploymentJournalWriter.AddJournalEntry(runningDeployment, false, pathToPackage);

                if (e is KubernetesDeploymentFailedException || e is TimeoutException)
                {
                    return -1;
                }

                throw;
            }

            return 0;
        }
    }
}
#endif