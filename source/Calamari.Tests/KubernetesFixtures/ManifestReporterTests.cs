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
using Calamari.Tests.Helpers;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using YamlDotNet.RepresentationModel;

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
            var namespaceResolver = Substitute.For<IKubernetesManifestNamespaceResolver>();

            var yaml = @"foo: bar";
            using (CreateFile(yaml, out var filePath))
            {
                var mr = new ManifestReporter(variables, CalamariPhysicalFileSystem.GetPhysicalFileSystem(), memoryLog, namespaceResolver);

                mr.ReportManifestFileApplied(filePath);

                memoryLog.ServiceMessages.Should().BeEmpty();
            }
        }

        [TestCase(nameof(FeatureToggle.KubernetesLiveObjectStatusFeatureToggle))]
        [TestCase(OctopusFeatureToggles.KnownSlugs.KubernetesObjectManifestInspection)]
        public void GivenValidYaml_ShouldPostSingleServiceMessage(string enabledFeatureToggle)
        {
            var memoryLog = new InMemoryLog();
            var variables = new CalamariVariables();
            variables.Set(KnownVariables.EnabledFeatureToggles, enabledFeatureToggle);

            var namespaceResolver = Substitute.For<IKubernetesManifestNamespaceResolver>();
            namespaceResolver.ResolveNamespace(Arg.Any<YamlMappingNode>(), Arg.Any<IVariables>())
                             .Returns("default");

            //Test that quotes are preserved, especially for numbers
            var yaml = @"name: George Washington
alphafield: ""fgdsfsd""
unquoted_int: 89
quoted_int: ""89""
unquoted_float: 5.75
quoted_float: ""5.75""
".ReplaceLineEndings();

            using (CreateFile(yaml, out var filePath))
            {
                var mr = new ManifestReporter(variables, CalamariPhysicalFileSystem.GetPhysicalFileSystem(), memoryLog, namespaceResolver);

                mr.ReportManifestFileApplied(filePath);

                var expected = ServiceMessage.Create(SpecialVariables.ServiceMessages.ManifestApplied.Name, ("ns", "default"), ("manifest", yaml));
                memoryLog.ServiceMessages.Should().BeEquivalentTo(new List<ServiceMessage> { expected });
            }
        }

        [TestCase(nameof(FeatureToggle.KubernetesLiveObjectStatusFeatureToggle))]
        [TestCase(OctopusFeatureToggles.KnownSlugs.KubernetesObjectManifestInspection)]
        public void GivenInvalidManifest_ShouldNotPostServiceMessage(string enabledFeatureToggle)
        {
            var memoryLog = new InMemoryLog();
            var variables = new CalamariVariables();
            variables.Set(KnownVariables.EnabledFeatureToggles, enabledFeatureToggle);

            var namespaceResolver = Substitute.For<IKubernetesManifestNamespaceResolver>();

            var yaml = @"text - Bar";
            using (CreateFile(yaml, out var filePath))
            {
                var mr = new ManifestReporter(variables, CalamariPhysicalFileSystem.GetPhysicalFileSystem(), memoryLog, namespaceResolver);

                mr.ReportManifestFileApplied(filePath);

                memoryLog.ServiceMessages.Should().BeEmpty();
            }
        }

        [TestCase(nameof(FeatureToggle.KubernetesLiveObjectStatusFeatureToggle))]
        [TestCase(OctopusFeatureToggles.KnownSlugs.KubernetesObjectManifestInspection)]
        public void NamespacedResolved_ShouldReportResolvedNamespace(string enabledFeatureToggle)
        {
            var memoryLog = new InMemoryLog();
            var variables = new CalamariVariables();
            variables.Set(KnownVariables.EnabledFeatureToggles, enabledFeatureToggle);

            var namespaceResolver = Substitute.For<IKubernetesManifestNamespaceResolver>();
            namespaceResolver.ResolveNamespace(Arg.Any<YamlMappingNode>(), Arg.Any<IVariables>())
                             .Returns("my-cool-namespace");

            var yaml = @"metadata:
  name: game-demo
  namespace: my-cool-namespace";
            using (CreateFile(yaml, out var filePath))
            {
                var variableNs = Some.String();
                variables.Set(SpecialVariables.Namespace, variableNs);
                var mr = new ManifestReporter(variables, CalamariPhysicalFileSystem.GetPhysicalFileSystem(), memoryLog, namespaceResolver);

                mr.ReportManifestFileApplied(filePath);

                memoryLog.ServiceMessages.First().Properties.Should().Contain(new KeyValuePair<string, string>("ns", "my-cool-namespace"));
            }
        }


        [TestCase(nameof(FeatureToggle.KubernetesLiveObjectStatusFeatureToggle))]
        [TestCase(OctopusFeatureToggles.KnownSlugs.KubernetesObjectManifestInspection)]
        public void GivenValidYamlString_ShouldPostSingleServiceMessage(string enabledFeatureToggle)
        {
            var memoryLog = new InMemoryLog();
            var variables = new CalamariVariables();
            variables.Set(KnownVariables.EnabledFeatureToggles, enabledFeatureToggle);

            var namespaceResolver = Substitute.For<IKubernetesManifestNamespaceResolver>();
            namespaceResolver.ResolveNamespace(Arg.Any<YamlMappingNode>(), Arg.Any<IVariables>())
                             .Returns("default");

            const string yaml = "foo: bar";
            var expectedYaml = $"foo: bar{Environment.NewLine}";
            var mr = new ManifestReporter(variables, CalamariPhysicalFileSystem.GetPhysicalFileSystem(), memoryLog, namespaceResolver);

            mr.ReportManifestApplied(yaml);

            var expected = ServiceMessage.Create(SpecialVariables.ServiceMessages.ManifestApplied.Name, ("ns", "default"), ("manifest", expectedYaml));
            memoryLog.ServiceMessages.Should().BeEquivalentTo(new List<ServiceMessage> { expected });
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