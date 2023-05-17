#if !NET40
using System;
using System.Collections.Generic;
using System.IO;
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
using Calamari.FeatureToggles;
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

        protected abstract IEnumerable<IInstallConvention> CommandSpecificConventions();

        public override int Execute(string[] commandLineArguments)
        {
            if (!FeatureToggle.MultiGlobPathsForRawYamlFeatureToggle.IsEnabled(variables))
                throw new InvalidOperationException(
                    "Unable to execute the Kubernetes Apply Raw YAML Command because the appropriate feature has not been enabled.");

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

            conventions.AddRange(CommandSpecificConventions());

            var runningDeployment = new RunningDeployment(pathToPackage, variables);

            var conventionRunner = new ConventionProcessor(runningDeployment, conventions, log);
            try
            {
                conventionRunner.RunConventions();
                deploymentJournalWriter.AddJournalEntry(runningDeployment, true, pathToPackage);
            }
            catch (Exception)
            {
                deploymentJournalWriter.AddJournalEntry(runningDeployment, false, pathToPackage);
                throw;
            }

            return 0;
        }
    }
}
#endif