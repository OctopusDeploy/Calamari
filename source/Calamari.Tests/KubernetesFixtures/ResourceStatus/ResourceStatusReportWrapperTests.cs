using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;
using Calamari.Kubernetes.Integration;
using Calamari.Kubernetes.ResourceStatus;
using Calamari.Kubernetes.ResourceStatus.Resources;
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

        public void FindsCorrectManifestFiles()
        {
            
        }

        public void FindsConfigMapsDeployedInADeployContainerStep()
        {
            
        }

        public void FindsSecretsDeployedInADeployContainerStep()
        {
            
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
}

