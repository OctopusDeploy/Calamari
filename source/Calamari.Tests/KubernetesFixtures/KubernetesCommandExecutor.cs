using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Aws.Integration;
using Calamari.Commands;
using Calamari.Common.Commands;
using Calamari.Common.Features.Behaviours;
using Calamari.Common.Features.ConfigurationTransforms;
using Calamari.Common.Features.ConfigurationVariables;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.StructuredVariables;
using Calamari.Common.Features.Substitutions;
using Calamari.Common.Plumbing.Deployment.Journal;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Kubernetes;
using Calamari.Kubernetes.Commands;
using Calamari.Kubernetes.Conventions;
using Calamari.Kubernetes.Integration;
using Calamari.Kubernetes.ResourceStatus;
using Calamari.Testing.Helpers;
using Calamari.Tests.Helpers;

namespace Calamari.Tests.KubernetesFixtures
{
    public class KubernetesCommandExecutor
    {
        private readonly ILog log;
        private readonly IVariables variables;
        private readonly Func<Dictionary<string, string>> getEnvironmentVariables;

        public KubernetesCommandExecutor(ILog log, IVariables variables,
            Func<Dictionary<string, string>> getEnvironmentVariables)
        {
            this.log = log;
            this.variables = variables;
            this.getEnvironmentVariables = getEnvironmentVariables;
        }

        public CalamariResult ExecuteTestKubernetesCommand(ICalamariFileSystem fileSystem)
        {
            return ExecuteCommand<TestableKubernetesDeploymentCommand>(fileSystem);
        }

        public CalamariResult ExecuteApplyRawYamlCommand(ICalamariFileSystem fileSystem)
        {

            return ExecuteCommand<KubernetesApplyRawYamlCommand>(fileSystem,
                (_,k) => () => CreateGatherAndApplyRawYamlConvention(fileSystem, k),
                (c,k) => () => CreateResourceStatusReportConvention(fileSystem, c, k));
        }

        public CalamariResult ExecuteWithRunScriptCommand(ICalamariFileSystem fileSystem, IEnumerable<IScriptWrapper> scriptWrappers)
        {
            using var _ = new EnvironmentVariableSetter(getEnvironmentVariables);

            var command = new RunScriptCommand(log,
                new DeploymentJournalWriter(fileSystem),
                variables,
                new ScriptEngine(scriptWrappers),
                fileSystem,
                CreateCommandLineRunner(),
                CreateSubstituteInFiles(fileSystem),
                CreateStructuredConfigVariablesService(fileSystem));

            return new CalamariResult(command.Execute(Array.Empty<string>()), new CaptureCommandInvocationOutputSink());
        }

        CalamariResult ExecuteCommand<TCommand>(ICalamariFileSystem fileSystem, params Func<CommandLineRunner, Kubectl, object>[] childArgs) where TCommand : KubernetesDeploymentCommandBase
        {
            var commandLineRunner = CreateCommandLineRunner();
            var kubectl = new Kubectl(variables, log, commandLineRunner);
            using var _ = new EnvironmentVariableSetter(getEnvironmentVariables);
            var args = new List<object>
            {
                new DeploymentJournalWriter(fileSystem),
                variables,
                kubectl,
                (DelegateInstallConvention.Factory)(d => new DelegateInstallConvention(d)),
                () => CreateSubstituteInFilesConvention(fileSystem),
                () => CreateConfigurationTransformsConvention(fileSystem),
                () => CreateConfigurationVariablesConvention(fileSystem),
                () => CreateStructuredConfigurationVariablesConvention(fileSystem),
                CreateAwsAuthConventionFactoryLazy(),
                () => CreateKubernetesAuthContextConvention(commandLineRunner, kubectl),
                (ConventionProcessor.Factory)((d, c) => new ConventionProcessor(d, c, log)),
                (RunningDeployment.Factory)(p => new RunningDeployment(p, variables)),
                fileSystem,
                CreateExtractPackage(fileSystem, commandLineRunner)
            };
            args.AddRange(childArgs.Select(a => a(commandLineRunner, kubectl)));

            var argTypes = new[]
            {
                typeof(IDeploymentJournalWriter),
                typeof(IVariables),
                typeof(Kubectl),
                typeof(DelegateInstallConvention.Factory),
                typeof(Func<SubstituteInFilesConvention>),
                typeof(Func<ConfigurationTransformsConvention>),
                typeof(Func<ConfigurationVariablesConvention>),
                typeof(Func<StructuredConfigurationVariablesConvention>),
                typeof(Lazy<AwsAuthConventionFactoryWrapper>),
                typeof(Func<KubernetesAuthContextConvention>),
                typeof(ConventionProcessor.Factory),
                typeof(RunningDeployment.Factory),
                typeof(ICalamariFileSystem),
                typeof(IExtractPackage)
            }.Concat(childArgs.Select(a => a.GetType())).ToArray();

            var command = (TCommand)typeof(TCommand)
                                    .GetConstructor(argTypes)
                                    ?.Invoke(args.ToArray());
            if (command is null)
            {
                throw new InvalidOperationException(
                    $"Unable to create instance of {typeof(TCommand).Name} as " +
                    $"args {string.Join(",", argTypes.Select(a => a.Name))}");
            }

            return new CalamariResult(command.Execute(Array.Empty<string>()), new CaptureCommandInvocationOutputSink());
        }

        private ISubstituteInFiles CreateSubstituteInFiles(ICalamariFileSystem fileSystem)
        {
            return new SubstituteInFiles(log, fileSystem, new FileSubstituter(log, fileSystem), variables);
        }

        private ResourceStatusReportConvention CreateResourceStatusReportConvention(ICalamariFileSystem fileSystem, CommandLineRunner commandLineRunner, Kubectl kubectl)
        {
            return new ResourceStatusReportConvention(new ResourceStatusReportExecutor(variables, log, fileSystem,
                new ResourceStatusChecker(new ResourceRetriever(new KubectlGet()),
                    new ResourceUpdateReporter(variables, log), log)), commandLineRunner, kubectl);
        }

        private GatherAndApplyRawYamlConvention CreateGatherAndApplyRawYamlConvention(ICalamariFileSystem fileSystem, Kubectl kubectl)
        {
            return new GatherAndApplyRawYamlConvention(log, fileSystem, kubectl);
        }

        private KubernetesAuthContextConvention CreateKubernetesAuthContextConvention(CommandLineRunner commandLineRunner, Kubectl kubectl)
        {
            return new KubernetesAuthContextConvention(log, commandLineRunner, kubectl);
        }

        private Lazy<AwsAuthConventionFactoryWrapper> CreateAwsAuthConventionFactoryLazy()
        {
            return new Lazy<AwsAuthConventionFactoryWrapper>(() =>
                new AwsAuthConventionFactoryWrapper(
                    new AwsAuthConventionFactory(_ => new AwsAuthConvention(log, variables))));
        }

        CommandLineRunner CreateCommandLineRunner()
        {
            return new CommandLineRunner(log, variables);
        }

        ExtractPackage CreateExtractPackage(ICalamariFileSystem fileSystem, CommandLineRunner commandLineRunner)
        {
            return new ExtractPackage(new CombinedPackageExtractor(log, variables, commandLineRunner), fileSystem,
                variables, log);
        }

        StructuredConfigurationVariablesConvention CreateStructuredConfigurationVariablesConvention(
            ICalamariFileSystem fileSystem)
        {
            return new StructuredConfigurationVariablesConvention(
                new StructuredConfigurationVariablesBehaviour(CreateStructuredConfigVariablesService(fileSystem)));
        }

        ConfigurationVariablesConvention CreateConfigurationVariablesConvention(ICalamariFileSystem fileSystem)
        {
            return new ConfigurationVariablesConvention(new ConfigurationVariablesBehaviour(fileSystem, variables,
                new ConfigurationVariablesReplacer(variables, log), log));
        }

        ConfigurationTransformsConvention CreateConfigurationTransformsConvention(ICalamariFileSystem fileSystem)
        {
            return new ConfigurationTransformsConvention(new ConfigurationTransformsBehaviour(fileSystem, variables,
                ConfigurationTransformer.FromVariables(variables, log), new TransformFileLocator(fileSystem, log),
                log));
        }

        SubstituteInFilesConvention CreateSubstituteInFilesConvention(ICalamariFileSystem fileSystem)
        {
            return new SubstituteInFilesConvention(new SubstituteInFilesBehaviour(CreateSubstituteInFiles(fileSystem)));
        }

        StructuredConfigVariablesService CreateStructuredConfigVariablesService(ICalamariFileSystem fileSystem)
        {
            return new StructuredConfigVariablesService(
                new PrioritisedList<IFileFormatVariableReplacer>(new IFileFormatVariableReplacer[]
                {
                    new JsonFormatVariableReplacer(fileSystem, log),
                    new XmlFormatVariableReplacer(fileSystem, log),
                    new YamlFormatVariableReplacer(fileSystem, log),
                    new PropertiesFormatVariableReplacer(fileSystem, log)
                }), variables, fileSystem, log);
        }
    }
}