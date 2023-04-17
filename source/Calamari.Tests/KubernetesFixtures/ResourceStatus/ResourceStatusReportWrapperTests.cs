using System.Collections.Generic;
using System.Linq;
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
            var statusChecker = new MockResourceStatusChecker();

            variables.Set(Deployment.SpecialVariables.EnabledFeatureToggles, "KubernetesDeploymentStatusFeatureToggle");
            variables.Set(SpecialVariables.ClusterUrl, "https://localhost");
            variables.Set(SpecialVariables.ResourceStatusCheck, "True");
            
            var wrapper = new ResourceStatusReportWrapper(variables, log, fileSystem, statusChecker);

            wrapper.IsEnabled(Syntax).Should().BeTrue();
        }

        [Test]
        public void NotEnabled_WhenDeployingToAKubernetesClusterWithStatusCheckDisabled()
        {
            var variables = new CalamariVariables();
            var log = new SilentLog();
            var fileSystem = new TestCalamariPhysicalFileSystem();
            var statusChecker = new MockResourceStatusChecker();

            variables.Set(Deployment.SpecialVariables.EnabledFeatureToggles, "KubernetesDeploymentStatusFeatureToggle");
            variables.Set(SpecialVariables.ClusterUrl, "https://localhost");

            var wrapper = new ResourceStatusReportWrapper(variables, log, fileSystem, statusChecker);

            wrapper.IsEnabled(Syntax).Should().BeFalse();
        }
        
        [Test]
        public void NotEnabled_WhenDoingABlueGreenDeployment()
        {
            var variables = new CalamariVariables();
            var log = new SilentLog();
            var fileSystem = new TestCalamariPhysicalFileSystem();
            var statusChecker = new MockResourceStatusChecker();

            variables.Set(Deployment.SpecialVariables.EnabledFeatureToggles, "KubernetesDeploymentStatusFeatureToggle");
            variables.Set(SpecialVariables.ClusterUrl, "https://localhost");
            variables.Set(SpecialVariables.ResourceStatusCheck, "True");
            variables.Set(SpecialVariables.DeploymentStyle, "bluegreen");
            
            var wrapper = new ResourceStatusReportWrapper(variables, log, fileSystem, statusChecker);

            wrapper.IsEnabled(Syntax).Should().BeFalse();
        }

        [Test]
        public void NotEnabled_WhenWaitForDeploymentIsSelected()
        {
            var variables = new CalamariVariables();
            var log = new SilentLog();
            var fileSystem = new TestCalamariPhysicalFileSystem();
            var statusChecker = new MockResourceStatusChecker();
            
            variables.Set(Deployment.SpecialVariables.EnabledFeatureToggles, "KubernetesDeploymentStatusFeatureToggle");
            variables.Set(SpecialVariables.ClusterUrl, "https://localhost");
            variables.Set(SpecialVariables.ResourceStatusCheck, "True");
            variables.Set(SpecialVariables.DeploymentWait, "wait");
            
            var wrapper = new ResourceStatusReportWrapper(variables, log, fileSystem, statusChecker);

            wrapper.IsEnabled(Syntax).Should().BeFalse();
        }

        [Test]
        public void FindsCorrectManifestFiles()
        {
            var variables = new CalamariVariables();
            var log = new SilentLog();
            var fileSystem = new TestCalamariPhysicalFileSystem();
            var statusChecker = new MockResourceStatusChecker();
            
            variables.Set(Deployment.SpecialVariables.EnabledFeatureToggles, "KubernetesDeploymentStatusFeatureToggle");
            variables.Set(SpecialVariables.ClusterUrl, "https://localhost");
            variables.Set(SpecialVariables.ResourceStatusCheck, "True");
            variables.Set(SpecialVariables.CustomResourceYamlFileName, "custom.yml");

            var testDirectory =
                TestEnvironment.GetTestPath("KubernetesFixtures", "ResourceStatus", "assets", "manifests");
            
            fileSystem.SetFileBasePath(testDirectory);
            
            var wrapper = new ResourceStatusReportWrapper(variables, log, fileSystem, statusChecker);
            wrapper.NextWrapper = new StubScriptWrapper();

            wrapper.ExecuteScript(
                new Script("stub"), 
                Syntax, 
                new CommandLineRunner(log, variables),
                new Dictionary<string, string>());

            statusChecker.CheckedResources.Should().BeEquivalentTo(new ResourceIdentifier[]
            {
                new ResourceIdentifier("Deployment", "deployment", "default"),
                new ResourceIdentifier("Ingress", "ingress", "default"),
                new ResourceIdentifier("Secret", "secret", "default"),
                new ResourceIdentifier("Service", "service", "default"),
                new ResourceIdentifier("CustomResource", "custom-resource", "default")
            });
        }

        [Test]
        public void FindsConfigMapsDeployedInADeployContainerStep()
        {
            var variables = new CalamariVariables();
            var log = new SilentLog();
            var fileSystem = new TestCalamariPhysicalFileSystem();
            var statusChecker = new MockResourceStatusChecker();

            const string configMapName = "ConfigMap-Deployment-01";
            variables.Set(Deployment.SpecialVariables.EnabledFeatureToggles, "KubernetesDeploymentStatusFeatureToggle");
            variables.Set(SpecialVariables.ClusterUrl, "https://localhost");
            variables.Set(SpecialVariables.ResourceStatusCheck, "True");
            variables.Set("Octopus.Action.KubernetesContainers.KubernetesConfigMapEnabled", "True");
            variables.Set("Octopus.Action.KubernetesContainers.ComputedConfigMapName", configMapName);

            var wrapper = new ResourceStatusReportWrapper(variables, log, fileSystem, statusChecker);
            wrapper.NextWrapper = new StubScriptWrapper();

            wrapper.ExecuteScript(
                new Script("stub"), 
                Syntax, 
                new CommandLineRunner(log, variables),
                new Dictionary<string, string>());

            statusChecker.CheckedResources.Should().BeEquivalentTo(new[]
            {
                new ResourceIdentifier("ConfigMap", configMapName, "default")
            });
        }

        [Test]
        public void FindsSecretsDeployedInADeployContainerStep()
        {
            var variables = new CalamariVariables();
            var log = new SilentLog();
            var fileSystem = new TestCalamariPhysicalFileSystem();
            var statusChecker = new MockResourceStatusChecker();

            const string secret = "Secret-Deployment-01";
            variables.Set(Deployment.SpecialVariables.EnabledFeatureToggles, "KubernetesDeploymentStatusFeatureToggle");
            variables.Set(SpecialVariables.ClusterUrl, "https://localhost");
            variables.Set(SpecialVariables.ResourceStatusCheck, "True");
            variables.Set("Octopus.Action.KubernetesContainers.KubernetesSecretEnabled", "True");
            variables.Set("Octopus.Action.KubernetesContainers.ComputedSecretName", secret);

            var wrapper = new ResourceStatusReportWrapper(variables, log, fileSystem, statusChecker);
            wrapper.NextWrapper = new StubScriptWrapper();

            wrapper.ExecuteScript(
                new Script("stub"), 
                Syntax, 
                new CommandLineRunner(log, variables),
                new Dictionary<string, string>());
            
            statusChecker.CheckedResources.Should().BeEquivalentTo(new[]
            {
                new ResourceIdentifier("Secret", secret, "default")
            });
        }
    }

    internal class MockResourceStatusChecker : IResourceStatusChecker
    {
        public List<ResourceIdentifier> CheckedResources { get; private set; }

        public bool CheckStatusUntilCompletionOrTimeout(
            IEnumerable<ResourceIdentifier> resourceIdentifiers, 
            IStabilizingTimer stabilizingTimer,
            Kubectl kubectl)
        {
            CheckedResources = resourceIdentifiers.ToList();
            return true;
        }
    }

    internal class StubScriptWrapper : IScriptWrapper
    {
        public int Priority { get; } = ScriptWrapperPriorities.TerminalScriptPriority;
        public IScriptWrapper NextWrapper { get; set; }
        public bool IsEnabled(ScriptSyntax syntax) => true;

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
