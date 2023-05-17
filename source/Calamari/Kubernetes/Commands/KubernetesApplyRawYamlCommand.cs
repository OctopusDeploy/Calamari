using System;
using System.Collections.Generic;
using System.IO;
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
using Calamari.Kubernetes.ResourceStatus;

namespace Calamari.Kubernetes.Commands
{
    [Command(Name, Description = "Apply Raw Yaml to Kubernetes Cluster")]
    public class KubernetesApplyRawYamlCommand : Command
    {
        private const string Name = "kubernetes-apply-raw-yaml";

        private readonly ILog log;
        private readonly IDeploymentJournalWriter deploymentJournalWriter;
        private readonly IVariables variables;
        private readonly ICommandLineRunner commandLineRunner;
        private readonly ICalamariFileSystem fileSystem;
        private readonly IExtractPackage extractPackage;
        private readonly ISubstituteInFiles substituteInFiles;
        private readonly IStructuredConfigVariablesService structuredConfigVariablesService;
        private readonly ResourceStatusReportExecutor statusReportExecutor;
        private readonly Lazy<AwsAuthConventionFactoryWrapper> awsAuthConventionFactoryFactory;

        private PathToPackage pathToPackage;

        public KubernetesApplyRawYamlCommand(
            ILog log,
            IDeploymentJournalWriter deploymentJournalWriter,
            IVariables variables,
            ICommandLineRunner commandLineRunner,
            ICalamariFileSystem fileSystem,
            IExtractPackage extractPackage,
            ISubstituteInFiles substituteInFiles,
            IStructuredConfigVariablesService structuredConfigVariablesService,
            ResourceStatusReportExecutor statusReportExecutor,
            Lazy<AwsAuthConventionFactoryWrapper> awsAuthConventionFactoryFactory)
        {
            this.log = log;
            this.deploymentJournalWriter = deploymentJournalWriter;
            this.variables = variables;
            this.commandLineRunner = commandLineRunner;
            this.fileSystem = fileSystem;
            this.extractPackage = extractPackage;
            this.substituteInFiles = substituteInFiles;
            this.structuredConfigVariablesService = structuredConfigVariablesService;
            this.statusReportExecutor = statusReportExecutor;
            this.awsAuthConventionFactoryFactory = awsAuthConventionFactoryFactory;
            Options.Add("package=", "Path to the NuGet package to install.", v => pathToPackage = new PathToPackage(Path.GetFullPath(v)));
        }
        public override int Execute(string[] commandLineArguments)
        {
            if (!FeatureToggle.MultiGlobPathsForRawYamlFeatureToggle.IsEnabled(variables))
                throw new InvalidOperationException(
                    "Unable to execute the Kubernetes Apply Raw YAML Command because the appropriate feature has not been enabled.");

            Options.Parse(commandLineArguments);
            var deployment = new RunningDeployment(pathToPackage, variables);
            var configurationTransformer = ConfigurationTransformer.FromVariables(variables, log);
            var transformFileLocator = new TransformFileLocator(fileSystem, log);
            var replacer = new ConfigurationVariablesReplacer(variables, log);
            var conventions = new List<IConvention>
            {
                new DelegateInstallConvention(d =>
                {
                    extractPackage.ExtractToStagingDirectory(pathToPackage);
                    //If using the inline yaml, copy it to the staging directory.
                    var inlineFile = Path.Combine(Environment.CurrentDirectory, "customresource.yml");
                    var stagingDirectory = Path.Combine(Environment.CurrentDirectory, "staging");
                    if (fileSystem.FileExists(inlineFile))
                    {
                        fileSystem.CopyFile(inlineFile, Path.Combine(stagingDirectory, "customresource.yml"));
                    }
                    d.StagingDirectory = stagingDirectory;
                    d.CurrentDirectoryProvider = DeploymentWorkingDirectory.StagingDirectory;
                }),
                new SubstituteInFilesConvention(new SubstituteInFilesBehaviour(substituteInFiles)),
                new ConfigurationTransformsConvention(new ConfigurationTransformsBehaviour(fileSystem, variables, configurationTransformer, transformFileLocator, log)),
                new ConfigurationVariablesConvention(new ConfigurationVariablesBehaviour(fileSystem, variables, replacer, log)),
                new StructuredConfigurationVariablesConvention(new StructuredConfigurationVariablesBehaviour(structuredConfigVariablesService)),
            };

            if (variables.Get(Deployment.SpecialVariables.Account.AccountType) == "AmazonWebServicesAccount")
            {
                conventions.Add(awsAuthConventionFactoryFactory.Value.Create());
            }

            conventions.AddRange(new IInstallConvention[]
            {
                new KubernetesAuthContextConvention(log, commandLineRunner),
                new GatherAndApplyRawYamlConvention(log, fileSystem, commandLineRunner),
                new ResourceStatusReportConvention(statusReportExecutor, commandLineRunner)
            });

            var conventionRunner = new ConventionProcessor(deployment, conventions, log);
            try
            {
                conventionRunner.RunConventions();
                deploymentJournalWriter.AddJournalEntry(deployment, true, pathToPackage);
            }
            catch (Exception)
            {
                deploymentJournalWriter.AddJournalEntry(deployment, false, pathToPackage);
                throw;
            }

            return 0;
        }
    }
}