using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.ServiceMessages;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures
{
    [TestFixture]
    public class ManifestReporterTests
    {
        [Test]
        public void GivenDisabledFeatureToggle_ShouldNotPostServiceMessage()
        {
            var memoryLog = new InMemoryLog();
            var variables = new CalamariVariables();

            var yaml = @"foo: bar";
            using (CreateFile(yaml, out var filePath))
            {
                var mr = new ManifestReporter(variables, CalamariPhysicalFileSystem.GetPhysicalFileSystem(), memoryLog);

                mr.ReportManifestApplied(filePath);

                memoryLog.ServiceMessages.Should().BeEmpty();
            }
        }
        
        [TestCase(nameof(FeatureToggle.KubernetesLiveObjectStatusFeatureToggle))]
        [TestCase( OctopusFeatureToggles.KnownSlugs.KubernetesObjectManifestInspection)]
        public void GivenValidYaml_ShouldPostSingleServiceMessage(string enabledFeatureToggle)
        {
            var memoryLog = new InMemoryLog();
            var variables = new CalamariVariables();
            variables.Set(KnownVariables.EnabledFeatureToggles, enabledFeatureToggle);

            var yaml = @"foo: bar";
            var expectedJson = "{\"foo\": \"bar\"}";
            using (CreateFile(yaml, out var filePath))
            {
                var mr = new ManifestReporter(variables, CalamariPhysicalFileSystem.GetPhysicalFileSystem(), memoryLog);

                mr.ReportManifestApplied(filePath);

                var expected = ServiceMessage.Create(SpecialVariables.ServiceMessageNames.ManifestApplied.Name, ("ns", "default"), ("manifest", expectedJson));
                memoryLog.ServiceMessages.Should().BeEquivalentTo(new List<ServiceMessage> { expected });
            }
        }

        [TestCase(nameof(FeatureToggle.KubernetesLiveObjectStatusFeatureToggle))]
        [TestCase( OctopusFeatureToggles.KnownSlugs.KubernetesObjectManifestInspection)]
        public void GivenInValidManifest_ShouldNotPostServiceMessage(string enabledFeatureToggle)
        {
            var memoryLog = new InMemoryLog();
            var variables = new CalamariVariables();
            variables.Set(KnownVariables.EnabledFeatureToggles, enabledFeatureToggle);

            var yaml = @"text - Bar";
            using (CreateFile(yaml, out var filePath))
            {
                var mr = new ManifestReporter(variables, CalamariPhysicalFileSystem.GetPhysicalFileSystem(), memoryLog);

                mr.ReportManifestApplied(filePath);

                memoryLog.ServiceMessages.Should().BeEmpty();
            }
        }

        [TestCase(nameof(FeatureToggle.KubernetesLiveObjectStatusFeatureToggle))]
        [TestCase( OctopusFeatureToggles.KnownSlugs.KubernetesObjectManifestInspection)]
        public void GivenNamespaceInManifest_ShouldReportManifestNamespace(string enabledFeatureToggle)
        {
            var memoryLog = new InMemoryLog();
            var variables = new CalamariVariables();
            variables.Set(KnownVariables.EnabledFeatureToggles, enabledFeatureToggle);
            var yaml = @"metadata:
  name: game-demo
  namespace: XXX";
            using (CreateFile(yaml, out var filePath))
            {
                var variableNs = Some.String();
                variables.Set(SpecialVariables.Namespace, variableNs);
                var mr = new ManifestReporter(variables, CalamariPhysicalFileSystem.GetPhysicalFileSystem(), memoryLog);

                mr.ReportManifestApplied(filePath);

                memoryLog.ServiceMessages.First().Properties.Should().Contain(new KeyValuePair<string, string>("ns", "XXX"));
            }
        }

        [TestCase(nameof(FeatureToggle.KubernetesLiveObjectStatusFeatureToggle))]
        [TestCase( OctopusFeatureToggles.KnownSlugs.KubernetesObjectManifestInspection)]
        public void GivenNamespaceNotInManifest_ShouldReportVariableNamespace(string enabledFeatureToggle)
        {
            var memoryLog = new InMemoryLog();
            var variables = new CalamariVariables();
            variables.Set(KnownVariables.EnabledFeatureToggles, enabledFeatureToggle);
            var yaml = @"foo: bar";
            using (CreateFile(yaml, out var filePath))
            {
                var variableNs = Some.String();
                variables.Set(SpecialVariables.Namespace, variableNs);
                var mr = new ManifestReporter(variables, CalamariPhysicalFileSystem.GetPhysicalFileSystem(), memoryLog);

                mr.ReportManifestApplied(filePath);

                memoryLog.ServiceMessages.First().Properties.Should().Contain(new KeyValuePair<string, string>("ns", variableNs));
            }
        }

        [TestCase(nameof(FeatureToggle.KubernetesLiveObjectStatusFeatureToggle))]
        [TestCase( OctopusFeatureToggles.KnownSlugs.KubernetesObjectManifestInspection)]
        public void GiveNoNamespaces_ShouldDefaultNamespace(string enabledFeatureToggle)
        {
            var memoryLog = new InMemoryLog();
            var variables = new CalamariVariables();
            variables.Set(KnownVariables.EnabledFeatureToggles, enabledFeatureToggle);
            var yaml = @"foo: bar";
            using (CreateFile(yaml, out var filePath))
            {
                var mr = new ManifestReporter(variables, CalamariPhysicalFileSystem.GetPhysicalFileSystem(), memoryLog);

                mr.ReportManifestApplied(filePath);

                memoryLog.ServiceMessages.First().Properties.Should().Contain(new KeyValuePair<string, string>("ns", "default"));
            }
        }

        static IDisposable CreateFile(string yaml, out string filePath)
        {
            var tempDir = TemporaryDirectory.Create();
            filePath = Path.Combine(tempDir.DirectoryPath, $"{Guid.NewGuid():d}.tmp");
            File.WriteAllText(filePath, yaml);
            return tempDir;
        }
    }
}