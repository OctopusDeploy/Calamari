using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;
using Calamari.Testing.Helpers;
using Calamari.Tests.Fixtures.Integration.FileSystem;
using Calamari.Tests.Helpers;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures
{
    [TestFixture]
    public class ManifestReportScriptWrapperTests
    {
        const ScriptSyntax Syntax = ScriptSyntax.Bash;
        readonly IManifestReporter manifestReporter = Substitute.For<IManifestReporter>();
        
        [SetUp]
        public void Setup()
        {
            manifestReporter.ClearReceivedCalls();
        }
        
        [Test]
        public void NotEnabled_WhenNotDeployingToAKubernetesCluster()
        {
            var variables = new CalamariVariables();
            
            var wrapper = CreateManifestReportScriptWrapper(variables);
            
            wrapper.IsEnabled(Syntax).Should().BeFalse();
        }
        
        [TestCase(SpecialVariables.ClusterUrl, "MyClusterUrl")]
        [TestCase(MachineVariables.DeploymentTargetType, "KubernetesTentacle")]
        [TestCase(SpecialVariables.AksClusterName, "Aks")]
        [TestCase(SpecialVariables.EksClusterName, "Eks")]
        [TestCase(SpecialVariables.GkeClusterName, "Gke")]
        public void Enabled_WhenDeployingToAKubernetesCluster(string variableName, string variableValue)
        {
            var variables = new CalamariVariables();
            variables.Set(variableName, variableValue);

            var wrapper = CreateManifestReportScriptWrapper(variables);

            wrapper.IsEnabled(Syntax).Should().BeTrue();
        }
        
        [Test]
        public void FindsAndReportsCorrectManifestFiles()
        {
            var variables = new CalamariVariables();
            variables.Set(SpecialVariables.CustomResourceYamlFileName, "custom.yml");

            var fileSystem = new TestCalamariPhysicalFileSystem();
            var testDirectory = TestEnvironment.GetTestPath("KubernetesFixtures", "ResourceStatus", "assets", "manifests");
            fileSystem.SetFileBasePath(testDirectory);

            var wrapper = CreateManifestReportScriptWrapper(variables, fileSystem);

            wrapper.ExecuteScript(
                                  new Script("stub"),
                                  Syntax,
                                  new CommandLineRunner(new SilentLog(), variables),
                                  new Dictionary<string, string>());

            var expectedManifests = GetExpectedManifests(testDirectory).ToArray();
            expectedManifests.Count().Should().BePositive();
            foreach (var expectedManifest in expectedManifests)
            {
                manifestReporter.Received().ReportManifestApplied(expectedManifest);
            }
        }

        static IEnumerable<string> GetExpectedManifests(string testDirectory) => Directory.GetFiles(testDirectory).Select(File.ReadAllText);

        ManifestReportScriptWrapper CreateManifestReportScriptWrapper(IVariables variables, ICalamariFileSystem fileSystem = null)
        {
            fileSystem = fileSystem ?? new TestCalamariPhysicalFileSystem();
            var manifestRetriever = new ManifestRetriever(variables, fileSystem);
            
            return new ManifestReportScriptWrapper(variables, fileSystem, manifestRetriever, manifestReporter)
            {
                NextWrapper = new StubScriptWrapper().Enable()
            };
        }
    }
}