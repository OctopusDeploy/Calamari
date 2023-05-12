#if NETCORE
using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Aws.Integration;
using Calamari.Aws.Kubernetes.Discovery;
using Calamari.Commands;
using Calamari.Common.Commands;
using Calamari.Common.Features.Behaviours;
using Calamari.Common.Features.ConfigurationTransforms;
using Calamari.Common.Features.ConfigurationVariables;
using Calamari.Common.Features.Discovery;
using Calamari.Common.Features.EmbeddedResources;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Features.StructuredVariables;
using Calamari.Common.Features.Substitutions;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Deployment.Journal;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.ServiceMessages;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Kubernetes;
using Calamari.Kubernetes.Commands;
using Calamari.Kubernetes.Conventions;
using Calamari.Kubernetes.Integration;
using Calamari.Kubernetes.ResourceStatus;
using Calamari.Testing.Helpers;
using Calamari.Tests.Fixtures.Integration.FileSystem;
using Calamari.Tests.Helpers;
using FluentAssertions;
using Newtonsoft.Json;
using NUnit.Framework;
using KubernetesSpecialVariables = Calamari.Kubernetes.SpecialVariables;
using SpecialVariables = Calamari.Deployment.SpecialVariables;

namespace Calamari.Tests.KubernetesFixtures
{
    public abstract class KubernetesContextScriptWrapperLiveFixtureBase : CalamariFixture
    {
        protected const string TestNamespace = "calamari-testing";

        protected IVariables variables;
        protected string testFolder;

        protected ILog redactionLog;

        [OneTimeSetUp]
        public void SetupTests()
        {
            testFolder = Path.GetDirectoryName(GetType().Assembly.FullLocalPath());
        }

        [SetUp]
        public void Setup()
        {
            variables = new CalamariVariables();

            redactionLog = new RedactedValuesLogger(Log = new DoNotDoubleLog());

            SetTestClusterVariables();
        }

        protected KubernetesContextScriptWrapper CreateWrapper(ICalamariFileSystem fileSystem = null)
        {
            return new KubernetesContextScriptWrapper(variables, redactionLog, new AssemblyEmbeddedResources(), fileSystem ?? new TestCalamariPhysicalFileSystem());
        }

        void SetTestClusterVariables()
        {

            variables.Set(KubernetesSpecialVariables.Namespace, TestNamespace);
            variables.Set(ScriptVariables.Syntax, CalamariEnvironment.IsRunningOnWindows ? ScriptSyntax.PowerShell.ToString() : ScriptSyntax.Bash.ToString());
        }

        CalamariResult ExecuteScript(IScriptWrapper wrapper, string scriptName) =>
            ExecuteScript(new[] { wrapper }, scriptName);

        CalamariResult ExecuteScript(IReadOnlyList<IScriptWrapper> additionalWrappers, string scriptName, ICalamariFileSystem fileSystem = null)
        {
            var wrappers = new List<IScriptWrapper>(additionalWrappers);
            if (variables.Get(SpecialVariables.Account.AccountType) == "AmazonWebServicesAccount")
            {
                wrappers.Add(new AwsScriptWrapper(redactionLog, variables));
            }

            var result = fileSystem != null
                ? ExecuteWithRunScriptCommand(fileSystem, wrappers)
                : ExecuteDirectlyWithScriptEngine(wrappers, scriptName);

            WriteLogMessagesToTestOutput();

            return result;
        }

        CalamariResult ExecuteApplyRawYamlCommand(ICalamariFileSystem fileSystem)
        {
            var commandLineRunner = CreateCommandLineRunner();
            var kubectl = new Kubectl(variables, redactionLog, commandLineRunner);
            var command = new KubernetesApplyRawYamlCommand(new DeploymentJournalWriter(fileSystem),
                variables,
                kubectl,
                d => new DelegateInstallConvention(d),
                () => CreateSubstituteInFilesConvention(fileSystem),
                () => CreateConfigurationTransformsConvention(fileSystem),
                () => CreateConfigurationVariablesConvention(fileSystem),
                () => CreateStructuredConfigurationVariablesConvention(fileSystem),
                CreateAwsAuthConvention,
                () => CreateKubernetesAuthContextConvention(commandLineRunner, kubectl),
                () => CreateGatherAndApplyRawYamlConvention(fileSystem, kubectl),
                () => CreateResourceStatusReportConvention(fileSystem, commandLineRunner, kubectl),
                (d, c) => new ConventionProcessor(d, c, redactionLog),
                p => new RunningDeployment(p, variables),
                fileSystem,
                CreateExtractPackage(fileSystem, commandLineRunner));

            return new CalamariResult(command.Execute(Array.Empty<string>()), new CaptureCommandInvocationOutputSink());
        }

        CalamariResult ExecuteWithRunScriptCommand(ICalamariFileSystem fileSystem, IEnumerable<IScriptWrapper> scriptWrappers)
        {
            var command = new RunScriptCommand(redactionLog,
                new DeploymentJournalWriter(fileSystem),
                variables,
                new ScriptEngine(scriptWrappers),
                fileSystem,
                CreateCommandLineRunner(),
                CreateSubstituteInFiles(fileSystem),
                CreateStructuredConfigVariablesService(fileSystem));

            return new CalamariResult(command.Execute(Array.Empty<string>()), new CaptureCommandInvocationOutputSink());
        }

        private ISubstituteInFiles CreateSubstituteInFiles(ICalamariFileSystem fileSystem)
        {
            return new SubstituteInFiles(redactionLog, fileSystem, new FileSubstituter(redactionLog, fileSystem), variables);
        }

        private ResourceStatusReportConvention CreateResourceStatusReportConvention(ICalamariFileSystem fileSystem, CommandLineRunner commandLineRunner, Kubectl kubectl)
        {
            return new ResourceStatusReportConvention(new ResourceStatusReportExecutor(variables, redactionLog, fileSystem,
                new ResourceStatusChecker(new ResourceRetriever(new KubectlGet()),
                    new ResourceUpdateReporter(variables, redactionLog), redactionLog)), commandLineRunner, kubectl);
        }

        private GatherAndApplyRawYamlConvention CreateGatherAndApplyRawYamlConvention(ICalamariFileSystem fileSystem, Kubectl kubectl)
        {
            return new GatherAndApplyRawYamlConvention(redactionLog, fileSystem, kubectl);
        }

        private KubernetesAuthContextConvention CreateKubernetesAuthContextConvention(CommandLineRunner commandLineRunner, Kubectl kubectl)
        {
            return new KubernetesAuthContextConvention(redactionLog, commandLineRunner, kubectl);
        }

        private AwsAuthConvention CreateAwsAuthConvention()
        {
            return new AwsAuthConvention(redactionLog);
        }

        CommandLineRunner CreateCommandLineRunner()
        {
            return new CommandLineRunner(redactionLog, variables);
        }

        ExtractPackage CreateExtractPackage(ICalamariFileSystem fileSystem, CommandLineRunner commandLineRunner)
        {
            return new ExtractPackage(new CombinedPackageExtractor(redactionLog, variables, commandLineRunner), fileSystem,
                variables, redactionLog);
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
                new ConfigurationVariablesReplacer(variables, redactionLog), redactionLog));
        }

        ConfigurationTransformsConvention CreateConfigurationTransformsConvention(ICalamariFileSystem fileSystem)
        {
            return new ConfigurationTransformsConvention(new ConfigurationTransformsBehaviour(fileSystem, variables,
                ConfigurationTransformer.FromVariables(variables, redactionLog), new TransformFileLocator(fileSystem, redactionLog),
                redactionLog));
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
                    new JsonFormatVariableReplacer(fileSystem, redactionLog),
                    new XmlFormatVariableReplacer(fileSystem, redactionLog),
                    new YamlFormatVariableReplacer(fileSystem, redactionLog),
                    new PropertiesFormatVariableReplacer(fileSystem, redactionLog)
                }), variables, fileSystem, redactionLog);
        }

        CalamariResult ExecuteDirectlyWithScriptEngine(IReadOnlyList<IScriptWrapper> wrappers, string scriptName)
        {
            var commandLineRunner = new CommandLineRunner(redactionLog, variables);
            var engine = new ScriptEngine(wrappers);
            var result = engine.Execute(new Script(scriptName), variables, commandLineRunner, GetEnvironments());

            return new CalamariResult(result.ExitCode, new CaptureCommandInvocationOutputSink());
        }

        protected virtual Dictionary<string, string> GetEnvironments()
        {
            return new Dictionary<string, string>();
        }

        protected void TestScript(IScriptWrapper wrapper, string scriptName)
        {
            using (var dir = TemporaryDirectory.Create())
            {
                var folderPath = Path.Combine(dir.DirectoryPath, "Folder with spaces");

                using (var temp = new TemporaryFile(Path.Combine(folderPath, $"{scriptName}.{(variables.Get(ScriptVariables.Syntax) == ScriptSyntax.Bash.ToString() ? "sh" : "ps1")}")))
                {
                    Directory.CreateDirectory(folderPath);
                    File.WriteAllText(temp.FilePath, $"echo running target script...");

                    var output = ExecuteScript(wrapper, temp.FilePath);
                    output.AssertSuccess();
                }
            }
        }

        protected void DeployWithScriptAndVerifySuccess(IReadOnlyList<IScriptWrapper> wrappers,
            ICalamariFileSystem fileSystem, Action<TemporaryDirectory> addFilesAction = null)
        {
            SetupTempDirectoryAndVerifyResult(addFilesAction, () =>
            {
                var scriptPath = Path.Combine(testFolder, "KubernetesFixtures/Scripts");
                variables.Set(SpecialVariables.Action.Script.ScriptBodyBySyntax(ScriptSyntax.Bash),
                    File.ReadAllText(Path.Combine(scriptPath, "KubernetesDeployment.sh")));
                variables.Set(SpecialVariables.Action.Script.ScriptBodyBySyntax(ScriptSyntax.PowerShell),
                    File.ReadAllText(Path.Combine(scriptPath, "KubernetesDeployment.ps1")));

                return ExecuteScript(wrappers, null, fileSystem);
            });
        }

        protected void DeployWithRawYamlCommandAndVerifySuccess(ICalamariFileSystem fileSystem,
            Action<TemporaryDirectory> addFilesAction = null)
        {
            SetupTempDirectoryAndVerifyResult(addFilesAction, () => ExecuteApplyRawYamlCommand(fileSystem));
        }

        private void SetupTempDirectoryAndVerifyResult(Action<TemporaryDirectory> addFilesAction, Func<CalamariResult> func)
        {
            using (var dir = TemporaryDirectory.Create())
            {
                var folderPath = Path.Combine(dir.DirectoryPath, "TestFolder");
                Directory.CreateDirectory(folderPath);
                var currentDirectory = Environment.CurrentDirectory;
                Environment.CurrentDirectory = folderPath;

                try
                {
                    addFilesAction?.Invoke(dir);

                    var output = func();

                    output.AssertSuccess();

                    WriteLogMessagesToTestOutput();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
                finally
                {
                    Environment.CurrentDirectory = currentDirectory;
                }
            }
        }

        protected void TestScriptAndVerifyCluster(IScriptWrapper wrapper, string scriptName, string kubectlExe = "kubectl")
        {
            using (var dir = TemporaryDirectory.Create())
            {
                var folderPath = Path.Combine(dir.DirectoryPath, "Folder with spaces");

                using (var temp = new TemporaryFile(Path.Combine(folderPath, $"{scriptName}.{(variables.Get(ScriptVariables.Syntax) == ScriptSyntax.Bash.ToString() ? "sh" : "ps1")}")))
                {
                    Directory.CreateDirectory(folderPath);
                    File.WriteAllText(temp.FilePath, $"{kubectlExe} cluster-info");

                    var output = ExecuteScript(wrapper, temp.FilePath);
                    output.AssertSuccess();
                }
            }
        }

        protected void DoDiscovery(AwsAuthenticationDetails authenticationDetails)
        {
            var scope = new TargetDiscoveryScope("TestSpace",
                "Staging",
                "testProject",
                null,
                new[] { "discovery-role" },
                "WorkerPools-1",
                null);

            var targetDiscoveryContext =
                new TargetDiscoveryContext<AwsAuthenticationDetails>(scope,
                    authenticationDetails);

            var result =
                ExecuteDiscoveryCommand(targetDiscoveryContext,
                    new[]{"Calamari.Aws"}
                );

            result.AssertSuccess();
        }

        protected void DoDiscoveryAndAssertReceivedServiceMessageWithMatchingProperties(
            AwsAuthenticationDetails authenticationDetails,
            Dictionary<string,string> properties)
        {
            DoDiscovery(authenticationDetails);

            var expectedServiceMessage = new ServiceMessage(
                KubernetesDiscoveryCommand.CreateKubernetesTargetServiceMessageName,
                properties);

            Log.ServiceMessages.Should()
                .ContainSingle(s => s.Properties["name"] == properties["name"])
                .Which.Should()
                .BeEquivalentTo(expectedServiceMessage);
        }

        protected CalamariResult ExecuteDiscoveryCommand<TAuthenticationDetails>(
            TargetDiscoveryContext<TAuthenticationDetails> discoveryContext,
            IEnumerable<string> extensions,
            params (string key, string value)[] otherVariables)
            where TAuthenticationDetails : class, ITargetDiscoveryAuthenticationDetails
        {
            using (var variablesFile = new TemporaryFile(Path.GetTempFileName()))
            {
                variables.Add(KubernetesDiscoveryCommand.ContextVariableName, JsonConvert.SerializeObject(discoveryContext));
                foreach (var (key, value) in otherVariables)
                    variables.Add(key, value);

                variables.Save(variablesFile.FilePath);

                var result = InvokeInProcess(Calamari()
                       .Action(KubernetesDiscoveryCommand.Name)
                       .Argument("variables", variablesFile.FilePath)
                       .Argument("extensions", string.Join(',', extensions)));

                WriteLogMessagesToTestOutput();

                return result;
            }
        }

        private void WriteLogMessagesToTestOutput()
        {
            foreach (var message in Log.Messages)
            {
                Console.WriteLine($"[{message.Level}] {message.FormattedMessage}");
            }
        }
    }
}
#endif