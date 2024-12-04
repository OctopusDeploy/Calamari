using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.FileSystem;
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
    public class ResourceStatusReportScriptWrapperTests
    {
        const ScriptSyntax Syntax = ScriptSyntax.Bash;

        [Test]
        public void Enabled_WhenDeployingToAKubernetesClusterWithStatusCheckEnabled()
        {
            var variables = new CalamariVariables();
            AddKubernetesStatusCheckVariables(variables);

            var (wrapper, _) = CreateResourceStatusReportWrapperAndStatusCheckContainer(variables);

            wrapper.IsEnabled(Syntax).Should().BeTrue();
        }

        [Test]
        public void NotEnabled_WhenDeployingToAKubernetesClusterWithStatusCheckDisabled()
        {
            var variables = new CalamariVariables();
            variables.Set(KnownVariables.EnabledFeatureToggles, "KubernetesDeploymentStatusFeatureToggle");
            variables.Set(SpecialVariables.ClusterUrl, "https://localhost");

            var (wrapper, _) = CreateResourceStatusReportWrapperAndStatusCheckContainer(variables);

            wrapper.IsEnabled(Syntax).Should().BeFalse();
        }

        [Test]
        public void NotEnabled_WhenDoingABlueGreenDeployment()
        {
            var variables = new CalamariVariables();
            AddKubernetesStatusCheckVariables(variables);
            variables.Set(SpecialVariables.DeploymentStyle, "bluegreen");

            var (wrapper, _) = CreateResourceStatusReportWrapperAndStatusCheckContainer(variables);

            wrapper.IsEnabled(Syntax).Should().BeFalse();
        }

        [Test]
        public void NotEnabled_WhenWaitForDeploymentIsSelected()
        {
            var variables = new CalamariVariables();
            AddKubernetesStatusCheckVariables(variables);
            variables.Set(SpecialVariables.DeploymentWait, "wait");

            var (wrapper, _) = CreateResourceStatusReportWrapperAndStatusCheckContainer(variables);

            wrapper.IsEnabled(Syntax).Should().BeFalse();
        }

        [Test]
        public void FindsCorrectManifestFiles()
        {
            var variables = new CalamariVariables();
            AddKubernetesStatusCheckVariables(variables);
            variables.Set(SpecialVariables.CustomResourceYamlFileName, "custom.yml");

            var fileSystem = new TestCalamariPhysicalFileSystem();
            var testDirectory = TestEnvironment.GetTestPath("KubernetesFixtures", "ResourceStatus", "assets", "manifests");
            fileSystem.SetFileBasePath(testDirectory);

            var (wrapper, statusCheckContainer) = CreateResourceStatusReportWrapperAndStatusCheckContainer(variables, fileSystem);

            wrapper.ExecuteScript(
                                  new Script("stub"),
                                  Syntax,
                                  new CommandLineRunner(new SilentLog(), variables),
                                  new Dictionary<string, string>());

            statusCheckContainer.StatusCheck.Should().NotBeNull();
            statusCheckContainer.StatusCheck.CheckedResources.Should()
                                .BeEquivalentTo(
                                                new ResourceIdentifier(SupportedResourceGroupVersionKinds.DeploymentV1, "deployment", "default"),
                                                new ResourceIdentifier(SupportedResourceGroupVersionKinds.IngressV1, "ingress", "default"),
                                                new ResourceIdentifier(SupportedResourceGroupVersionKinds.SecretV1, "secret", "default"),
                                                new ResourceIdentifier(SupportedResourceGroupVersionKinds.ServiceV1, "service", "default"),
                                                new ResourceIdentifier(new ResourceGroupVersionKind(null, null, "CustomResource"), "custom-resource", "default"));
        }

        [Test]
        public void FindsConfigMapsDeployedInADeployContainerStepWhenConfigMapDataIsNotEmpty()
        {
            var variables = new CalamariVariables();
            const string configMapName = "ConfigMap-Deployment-01";
            AddKubernetesStatusCheckVariables(variables);
            variables.Set("Octopus.Action.KubernetesContainers.KubernetesConfigMapEnabled", "True");
            variables.Set("Octopus.Action.KubernetesContainers.ComputedConfigMapName", configMapName);
            variables.Set("Octopus.Action.KubernetesContainers.ConfigMapData[1].FileName", "1");

            var fileSystem = new TestCalamariPhysicalFileSystem();
            var tempDirectory = fileSystem.CreateTemporaryDirectory();
            fileSystem.SetFileBasePath(tempDirectory);

            var (wrapper, statusCheckContainer) = CreateResourceStatusReportWrapperAndStatusCheckContainer(variables, fileSystem);

            try
            {
                wrapper.ExecuteScript(
                                      new Script("stub"),
                                      Syntax,
                                      new CommandLineRunner(new SilentLog(), variables),
                                      new Dictionary<string, string>());

                statusCheckContainer.StatusCheck.Should().NotBeNull();
                statusCheckContainer.StatusCheck.CheckedResources.Should().BeEquivalentTo(new ResourceIdentifier(SupportedResourceGroupVersionKinds.ConfigMapV1, configMapName, "default"));
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
            const string configMapName = "ConfigMap-Deployment-01";
            AddKubernetesStatusCheckVariables(variables);
            variables.Set("Octopus.Action.KubernetesContainers.KubernetesConfigMapEnabled", "True");
            variables.Set("Octopus.Action.KubernetesContainers.ComputedConfigMapName", configMapName);

            var fileSystem = new TestCalamariPhysicalFileSystem();
            var tempDirectory = fileSystem.CreateTemporaryDirectory();
            fileSystem.SetFileBasePath(tempDirectory);

            var (wrapper, statusCheckContainer) = CreateResourceStatusReportWrapperAndStatusCheckContainer(variables, fileSystem);

            try
            {
                wrapper.ExecuteScript(
                                      new Script("stub"),
                                      Syntax,
                                      new CommandLineRunner(new SilentLog(), variables),
                                      new Dictionary<string, string>());

                statusCheckContainer.StatusCheck.Should().NotBeNull();
                statusCheckContainer.StatusCheck.CheckedResources.Should().BeEmpty();
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
            const string secret = "Secret-Deployment-01";
            AddKubernetesStatusCheckVariables(variables);
            variables.Set("Octopus.Action.KubernetesContainers.KubernetesSecretEnabled", "True");
            variables.Set("Octopus.Action.KubernetesContainers.ComputedSecretName", secret);
            variables.Set("Octopus.Action.KubernetesContainers.SecretData[1].FileName", "1");

            var fileSystem = new TestCalamariPhysicalFileSystem();
            var tempDirectory = fileSystem.CreateTemporaryDirectory();
            fileSystem.SetFileBasePath(tempDirectory);

            var (wrapper, statusCheckContainer) = CreateResourceStatusReportWrapperAndStatusCheckContainer(variables, fileSystem);

            try
            {
                wrapper.ExecuteScript(
                                      new Script("stub"),
                                      Syntax,
                                      new CommandLineRunner(new SilentLog(), variables),
                                      new Dictionary<string, string>());

                statusCheckContainer.StatusCheck.Should().NotBeNull();
                statusCheckContainer.StatusCheck.CheckedResources.Should()
                                    .BeEquivalentTo(new[]
                                    {
                                        new ResourceIdentifier(SupportedResourceGroupVersionKinds.SecretV1, secret, "default")
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
            const string secret = "Secret-Deployment-01";
            AddKubernetesStatusCheckVariables(variables);
            variables.Set("Octopus.Action.KubernetesContainers.KubernetesSecretEnabled", "True");
            variables.Set("Octopus.Action.KubernetesContainers.ComputedSecretName", secret);

            var fileSystem = new TestCalamariPhysicalFileSystem();
            var tempDirectory = fileSystem.CreateTemporaryDirectory();
            fileSystem.SetFileBasePath(tempDirectory);

            var (wrapper, statusCheckContainer) = CreateResourceStatusReportWrapperAndStatusCheckContainer(variables, fileSystem);

            try
            {
                wrapper.ExecuteScript(
                                      new Script("stub"),
                                      Syntax,
                                      new CommandLineRunner(new SilentLog(), variables),
                                      new Dictionary<string, string>());

                statusCheckContainer.StatusCheck.Should().NotBeNull();
                statusCheckContainer.StatusCheck.CheckedResources.Should().BeEmpty();
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
            AddKubernetesStatusCheckVariables(variables);
            variables.Set(SpecialVariables.Namespace, @namespace);

            var fileSystem = new TestCalamariPhysicalFileSystem();
            var testDirectory = TestEnvironment.GetTestPath("KubernetesFixtures", "ResourceStatus", "assets", "no-namespace");
            fileSystem.SetFileBasePath(testDirectory);

            var (wrapper, statusCheckContainer) = CreateResourceStatusReportWrapperAndStatusCheckContainer(variables, fileSystem);

            wrapper.ExecuteScript(
                                  new Script("stub"),
                                  Syntax,
                                  new CommandLineRunner(new SilentLog(), variables),
                                  new Dictionary<string, string>());

            statusCheckContainer.StatusCheck.Should().NotBeNull();
            statusCheckContainer.StatusCheck.CheckedResources.Should().BeEquivalentTo(new ResourceIdentifier(SupportedResourceGroupVersionKinds.DeploymentV1, "deployment", "default"));
        }

        static void AddKubernetesStatusCheckVariables(IVariables variables)
        {
            variables.Set(SpecialVariables.ResourceStatusCheck, "True");
        }

        static (ResourceStatusReportScriptWrapper, RunningStatusCheckContainer) CreateResourceStatusReportWrapperAndStatusCheckContainer(IVariables variables, ICalamariFileSystem fileSystem = null)
        {
            var log = new SilentLog();
            fileSystem = fileSystem ?? new TestCalamariPhysicalFileSystem();
            var kubectl = new Kubectl(variables, log, new CommandLineRunner(log, variables));
            var statusCheckerContainer = new RunningStatusCheckContainer();
            var reportExecutor =
                new ResourceStatusReportExecutor(variables,
                                                 (_, __, rs) =>
                                                 {
                                                     statusCheckerContainer.StatusCheck = new MockRunningResourceStatusCheck(rs);
                                                     return statusCheckerContainer.StatusCheck;
                                                 });
            var resourceFinder = new ResourceFinder(variables, new ManifestRetriever(variables, fileSystem));

            var wrapper = new ResourceStatusReportScriptWrapper(kubectl, variables, resourceFinder, reportExecutor)
            {
                NextWrapper = new StubScriptWrapper().Enable()
            };

            return (wrapper, statusCheckerContainer);
        }
    }

    // This class is used for capturing the status check which is created by a factory delegate.
    class RunningStatusCheckContainer
    {
        public MockRunningResourceStatusCheck StatusCheck { get; set; }
    }

    class MockRunningResourceStatusCheck : IRunningResourceStatusCheck
    {
        public MockRunningResourceStatusCheck(IEnumerable<ResourceIdentifier> initialResources)
        {
            CheckedResources.UnionWith(initialResources);
        }

        public HashSet<ResourceIdentifier> CheckedResources { get; } = new HashSet<ResourceIdentifier>();

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
}