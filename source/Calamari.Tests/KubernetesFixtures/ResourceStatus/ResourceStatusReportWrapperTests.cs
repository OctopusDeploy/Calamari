using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;
using Calamari.Kubernetes.Integration;
using Calamari.Kubernetes.ResourceStatus;
using Calamari.Kubernetes.ResourceStatus.Resources;
using Calamari.Testing.Helpers;
using Calamari.Tests.Fixtures.Integration.FileSystem;
using Calamari.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.ResourceStatus
{
    [TestFixture]
    public class ResourceStatusReportWrapperTests
    {
        private const ScriptSyntax Syntax = ScriptSyntax.Bash;

        [Test]
        public void Enabled_WhenDeployingToAKubernetesClusterWithStatusCheckEnabled()
        {
            var variables = new CalamariVariables();
            var log = new SilentLog();
            var fileSystem = new TestCalamariPhysicalFileSystem();
            var kubectl = new Kubectl(variables, log, new CommandLineRunner(log, variables));
            var reportExecutor =
                new ResourceStatusReportExecutor(variables, (_, __, rs) => new MockResourceStatusChecker(rs));

            AddKubernetesStatusCheckVariables(variables);

            var wrapper = new ResourceStatusReportWrapper(kubectl, variables, fileSystem, reportExecutor);

            wrapper.IsEnabled(Syntax).Should().BeTrue();
        }

        [Test]
        public void NotEnabled_WhenDeployingToAKubernetesClusterWithStatusCheckDisabled()
        {
            var variables = new CalamariVariables();
            var log = new SilentLog();
            var fileSystem = new TestCalamariPhysicalFileSystem();
            var kubectl = new Kubectl(variables, log, new CommandLineRunner(log, variables));
            var reportExecutor =
                new ResourceStatusReportExecutor(variables, (_, __, rs) => new MockResourceStatusChecker(rs));

            variables.Set(KnownVariables.EnabledFeatureToggles, "KubernetesDeploymentStatusFeatureToggle");
            variables.Set(SpecialVariables.ClusterUrl, "https://localhost");

            var wrapper = new ResourceStatusReportWrapper(kubectl, variables, fileSystem, reportExecutor);

            wrapper.IsEnabled(Syntax).Should().BeFalse();
        }

        [Test]
        public void NotEnabled_WhenDoingABlueGreenDeployment()
        {
            var variables = new CalamariVariables();
            var log = new SilentLog();
            var fileSystem = new TestCalamariPhysicalFileSystem();
            var kubectl = new Kubectl(variables, log, new CommandLineRunner(log, variables));
            var reportExecutor =
                new ResourceStatusReportExecutor(variables, (_, __, rs) => new MockResourceStatusChecker(rs));

            AddKubernetesStatusCheckVariables(variables);
            variables.Set(SpecialVariables.DeploymentStyle, "bluegreen");

            var wrapper = new ResourceStatusReportWrapper(kubectl, variables, fileSystem, reportExecutor);

            wrapper.IsEnabled(Syntax).Should().BeFalse();
        }

        [Test]
        public void NotEnabled_WhenWaitForDeploymentIsSelected()
        {
            var variables = new CalamariVariables();
            var log = new SilentLog();
            var fileSystem = new TestCalamariPhysicalFileSystem();
            var kubectl = new Kubectl(variables, log, new CommandLineRunner(log, variables));
            var reportExecutor =
                new ResourceStatusReportExecutor(variables, (_, __, rs) => new MockResourceStatusChecker(rs));

            AddKubernetesStatusCheckVariables(variables);
            variables.Set(SpecialVariables.DeploymentWait, "wait");

            var wrapper = new ResourceStatusReportWrapper(kubectl, variables, fileSystem, reportExecutor);

            wrapper.IsEnabled(Syntax).Should().BeFalse();
        }

         [Test]
         public void FindsCorrectManifestFiles()
         {
             var variables = new CalamariVariables();
             var log = new SilentLog();
             var fileSystem = new TestCalamariPhysicalFileSystem();
             var kubectl = new Kubectl(variables, log, new CommandLineRunner(log, variables));
             MockResourceStatusChecker statusChecker = null;
             var reportExecutor =
                 new ResourceStatusReportExecutor(variables, (_, __, rs) => statusChecker = new MockResourceStatusChecker(rs));

             AddKubernetesStatusCheckVariables(variables);
             variables.Set(SpecialVariables.CustomResourceYamlFileName, "custom.yml");

             var testDirectory =
                 TestEnvironment.GetTestPath("KubernetesFixtures", "ResourceStatus", "assets", "manifests");

             fileSystem.SetFileBasePath(testDirectory);

             var wrapper = new ResourceStatusReportWrapper(kubectl, variables, fileSystem, reportExecutor);
             wrapper.NextWrapper = new StubScriptWrapper().Enable();

             wrapper.ExecuteScript(
                 new Script("stub"),
                 Syntax,
                 new CommandLineRunner(log, variables),
                 new Dictionary<string, string>());

             statusChecker.Should().NotBeNull();
             statusChecker.CheckedResources.Should().BeEquivalentTo(
                 new ResourceIdentifier("apps", "Deployment", "deployment", "default"),
                 new ResourceIdentifier("networking.k8s.io", "Ingress", "ingress", "default"),
                 new ResourceIdentifier("", "Secret", "secret", "default"),
                 new ResourceIdentifier("", "Service", "service", "default"),
                 new ResourceIdentifier(null, "CustomResource", "custom-resource", "default"));
         }

         [Test]
         public void FindsConfigMapsDeployedInADeployContainerStepWhenConfigMapDataIsNotEmpty()
         {
             var variables = new CalamariVariables();
             var log = new SilentLog();
             var fileSystem = new TestCalamariPhysicalFileSystem();
             var kubectl = new Kubectl(variables, log, new CommandLineRunner(log, variables));
             MockResourceStatusChecker statusChecker = null;
             var reportExecutor =
                 new ResourceStatusReportExecutor(variables, (_, __, rs) => statusChecker = new MockResourceStatusChecker(rs));

             const string configMapName = "ConfigMap-Deployment-01";
             AddKubernetesStatusCheckVariables(variables);
             variables.Set("Octopus.Action.KubernetesContainers.KubernetesConfigMapEnabled", "True");
             variables.Set("Octopus.Action.KubernetesContainers.ComputedConfigMapName", configMapName);
             variables.Set("Octopus.Action.KubernetesContainers.ConfigMapData[1].FileName", "1");

             var tempDirectory = fileSystem.CreateTemporaryDirectory();
             try
             {
                 fileSystem.SetFileBasePath(tempDirectory);

                 var wrapper = new ResourceStatusReportWrapper(kubectl, variables, fileSystem, reportExecutor);
                 wrapper.NextWrapper = new StubScriptWrapper().Enable();

                 wrapper.ExecuteScript(
                     new Script("stub"),
                     Syntax,
                     new CommandLineRunner(log, variables),
                     new Dictionary<string, string>());

                 statusChecker.Should().NotBeNull();
                 statusChecker.CheckedResources.Should().BeEquivalentTo(new ResourceIdentifier("","ConfigMap", configMapName, "default"));
             }
             finally
             {
                 fileSystem.DeleteDirectory(tempDirectory);
             }
         }

         [Test]
         public void SkipsConfigMapsInADeployContainerStepWhenConfigMapDataIsNotSet()
         {
             var variables = new CalamariVariables();
             var log = new SilentLog();
             var fileSystem = new TestCalamariPhysicalFileSystem();
             var kubectl = new Kubectl(variables, log, new CommandLineRunner(log, variables));
             MockResourceStatusChecker statusChecker = null;
             var reportExecutor =
                 new ResourceStatusReportExecutor(variables, (_, __, rs) => statusChecker = new MockResourceStatusChecker(rs));

             const string configMapName = "ConfigMap-Deployment-01";
             AddKubernetesStatusCheckVariables(variables);
             variables.Set("Octopus.Action.KubernetesContainers.KubernetesConfigMapEnabled", "True");
             variables.Set("Octopus.Action.KubernetesContainers.ComputedConfigMapName", configMapName);

             var tempDirectory = fileSystem.CreateTemporaryDirectory();
             try
             {
                 fileSystem.SetFileBasePath(tempDirectory);

                 var wrapper = new ResourceStatusReportWrapper(kubectl, variables, fileSystem, reportExecutor);
                 wrapper.NextWrapper = new StubScriptWrapper().Enable();

                 wrapper.ExecuteScript(
                     new Script("stub"),
                     Syntax,
                     new CommandLineRunner(log, variables),
                     new Dictionary<string, string>());

                 statusChecker.Should().NotBeNull();
                 statusChecker.CheckedResources.Should().BeEmpty();
             }
             finally
             {
                 fileSystem.DeleteDirectory(tempDirectory);
             }
         }

         [Test]
         public void FindsSecretsDeployedInADeployContainerStepWhenSecretDataIsSet()
         {
             var variables = new CalamariVariables();
             var log = new SilentLog();
             var fileSystem = new TestCalamariPhysicalFileSystem();
             var kubectl = new Kubectl(variables, log, new CommandLineRunner(log, variables));
             MockResourceStatusChecker statusChecker = null;
             var reportExecutor =
                 new ResourceStatusReportExecutor(variables, (_, __, rs) => statusChecker = new MockResourceStatusChecker(rs));

             const string secret = "Secret-Deployment-01";
             AddKubernetesStatusCheckVariables(variables);
             variables.Set("Octopus.Action.KubernetesContainers.KubernetesSecretEnabled", "True");
             variables.Set("Octopus.Action.KubernetesContainers.ComputedSecretName", secret);
             variables.Set("Octopus.Action.KubernetesContainers.SecretData[1].FileName", "1");

             var tempDirectory = fileSystem.CreateTemporaryDirectory();
             try
             {
                 fileSystem.SetFileBasePath(tempDirectory);

                 var wrapper = new ResourceStatusReportWrapper(kubectl, variables, fileSystem, reportExecutor);
                 wrapper.NextWrapper = new StubScriptWrapper().Enable();

                 wrapper.ExecuteScript(
                     new Script("stub"),
                     Syntax,
                     new CommandLineRunner(log, variables),
                     new Dictionary<string, string>());

                 statusChecker.Should().NotBeNull();
                 statusChecker.CheckedResources.Should().BeEquivalentTo(new[]
                 {
                     new ResourceIdentifier("", "Secret", secret, "default")
                 });
             }
             finally
             {
                 fileSystem.DeleteDirectory(tempDirectory);
             }
         }

         [Test]
         public void SkipsSecretsInADeployContainerStepWhenSecretDataIsNotSet()
         {
             var variables = new CalamariVariables();
             var log = new SilentLog();
             var fileSystem = new TestCalamariPhysicalFileSystem();
             var kubectl = new Kubectl(variables, log, new CommandLineRunner(log, variables));
             MockResourceStatusChecker statusChecker = null;
             var reportExecutor =
                 new ResourceStatusReportExecutor(variables, (_, __, rs) => statusChecker = new MockResourceStatusChecker(rs));

             const string secret = "Secret-Deployment-01";
             AddKubernetesStatusCheckVariables(variables);
             variables.Set("Octopus.Action.KubernetesContainers.KubernetesSecretEnabled", "True");
             variables.Set("Octopus.Action.KubernetesContainers.ComputedSecretName", secret);

             var tempDirectory = fileSystem.CreateTemporaryDirectory();
             try
             {
                 fileSystem.SetFileBasePath(tempDirectory);

                 var wrapper = new ResourceStatusReportWrapper(kubectl, variables, fileSystem, reportExecutor);
                 wrapper.NextWrapper = new StubScriptWrapper().Enable();

                 wrapper.ExecuteScript(
                     new Script("stub"),
                     Syntax,
                     new CommandLineRunner(log, variables),
                     new Dictionary<string, string>());

                 statusChecker.Should().NotBeNull();
                 statusChecker.CheckedResources.Should().BeEmpty();
             }
             finally
             {
                 fileSystem.DeleteDirectory(tempDirectory);
             }
         }

         [TestCase(null)]
         [TestCase("")]
         public void SetNamespaceToDefaultWhenTheDefaultNamespaceIsNullOrAnEmptyString(string @namespace)
         {
             var variables = new CalamariVariables();
             var log = new SilentLog();
             var fileSystem = new TestCalamariPhysicalFileSystem();
             var kubectl = new Kubectl(variables, log, new CommandLineRunner(log, variables));
             MockResourceStatusChecker statusChecker = null;
             var reportExecutor =
                 new ResourceStatusReportExecutor(variables, (_, __, rs) => statusChecker = new MockResourceStatusChecker(rs));

             AddKubernetesStatusCheckVariables(variables);
             variables.Set(SpecialVariables.Namespace, @namespace);

             var testDirectory =
                 TestEnvironment.GetTestPath("KubernetesFixtures", "ResourceStatus", "assets", "no-namespace");

             fileSystem.SetFileBasePath(testDirectory);

             var wrapper = new ResourceStatusReportWrapper(kubectl, variables, fileSystem, reportExecutor);
             wrapper.NextWrapper = new StubScriptWrapper().Enable();

             wrapper.ExecuteScript(
                 new Script("stub"),
                 Syntax,
                 new CommandLineRunner(log, variables),
                 new Dictionary<string, string>());

             statusChecker.Should().NotBeNull();
             statusChecker.CheckedResources.Should().BeEquivalentTo(new ResourceIdentifier("apps","Deployment", "deployment", "default"));
         }

         static void AddKubernetesStatusCheckVariables(IVariables variables)
         {
             variables.Set(SpecialVariables.ResourceStatusCheck, "True");
         }
    }

    internal class MockResourceStatusChecker : IRunningResourceStatusCheck
    {
        public HashSet<ResourceIdentifier> CheckedResources { get; } = new HashSet<ResourceIdentifier>();

        public MockResourceStatusChecker(IEnumerable<ResourceIdentifier> initialResources)
        {
            CheckedResources.UnionWith(initialResources);
        }

        public async Task<bool> WaitForCompletionOrTimeout(CancellationToken cancellationToken)
        {
            return await Task.FromResult(true);
        }

        public async Task AddResources(ResourceIdentifier[] newResources)
        {
            await Task.CompletedTask;
            CheckedResources.UnionWith(newResources);
        }
    }

    internal class StubScriptWrapper : IScriptWrapper
    {
        private bool isEnabled = false;

        public int Priority { get; } = 1;
        public IScriptWrapper NextWrapper { get; set; }
        public bool IsEnabled(ScriptSyntax syntax) => isEnabled;

        // We manually enable this wrapper when needed,
        // to avoid this wrapper being auto-registered and called from real programs
        public StubScriptWrapper Enable()
        {
            isEnabled = true;
            return this;
        }

        public CommandResult ExecuteScript(
            Script script,
            ScriptSyntax scriptSyntax,
            ICommandLineRunner commandLineRunner,
            Dictionary<string, string> environmentVars)
        {
            return new CommandResult("stub", 0);
        }
    }
}
