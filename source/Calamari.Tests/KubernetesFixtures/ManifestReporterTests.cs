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
            var namespacedApiResourcesDict = new NamespacedApiResourcesDictBuilder().WithDefaults().Build();

            var yaml = @"foo: bar";
            using (CreateFile(yaml, out var filePath))
            {
                var mr = new ManifestReporter(variables, CalamariPhysicalFileSystem.GetPhysicalFileSystem(), memoryLog, namespacedApiResourcesDict);

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
            var namespacedApiResourcesDict = new NamespacedApiResourcesDictBuilder().WithDefaults().Build();

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
                var mr = new ManifestReporter(variables, CalamariPhysicalFileSystem.GetPhysicalFileSystem(), memoryLog, namespacedApiResourcesDict);

                mr.ReportManifestApplied(filePath);

                var expected = ServiceMessage.Create(SpecialVariables.ServiceMessageNames.ManifestApplied.Name, ("ns", "default"), ("manifest", yaml));
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
                var mr = new ManifestReporter(variables, CalamariPhysicalFileSystem.GetPhysicalFileSystem(), memoryLog, Substitute.For<IApiResourceScopeLookup>());

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
                var mr = new ManifestReporter(variables, CalamariPhysicalFileSystem.GetPhysicalFileSystem(), memoryLog, Substitute.For<IApiResourceScopeLookup>());

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
                var mr = new ManifestReporter(variables, CalamariPhysicalFileSystem.GetPhysicalFileSystem(), memoryLog, Substitute.For<IApiResourceScopeLookup>());

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
                var mr = new ManifestReporter(variables, CalamariPhysicalFileSystem.GetPhysicalFileSystem(), memoryLog, Substitute.For<IApiResourceScopeLookup>());

                mr.ReportManifestApplied(filePath);

                memoryLog.ServiceMessages.First().Properties.Should().Contain(new KeyValuePair<string, string>("ns", "default"));
            }
        }

        [TestCase(nameof(FeatureToggle.KubernetesLiveObjectStatusFeatureToggle))]
        [TestCase( OctopusFeatureToggles.KnownSlugs.KubernetesObjectManifestInspection)]
        public void GivenGlobalKind_NamespaceShouldBeNull(string enabledFeatureToggle)
        {
            var memoryLog = new InMemoryLog();
            var variables = new CalamariVariables();
            variables.Set(KnownVariables.EnabledFeatureToggles, enabledFeatureToggle);
            var namespacedApiResourcesDict = new NamespacedApiResourcesDictBuilder().WithDefaults().Build();

            var yaml = @"apiVersion: v1
kind: Namespace
metadata:
  name: test
";
            using (CreateFile(yaml, out var filePath))
            {
                var mr = new ManifestReporter(variables, CalamariPhysicalFileSystem.GetPhysicalFileSystem(), memoryLog, namespacedApiResourcesDict);

                mr.ReportManifestApplied(filePath);

                memoryLog.ServiceMessages.First().Properties.Should().Contain(new KeyValuePair<string, string>("ns", null));
            }
        }

        [TestCase(nameof(FeatureToggle.KubernetesLiveObjectStatusFeatureToggle))]
        [TestCase( OctopusFeatureToggles.KnownSlugs.KubernetesObjectManifestInspection)]
        public void GivenNamespacedKind_NamespaceShouldBeDefault(string enabledFeatureToggle)
        {
            var memoryLog = new InMemoryLog();
            var variables = new CalamariVariables();
            variables.Set(KnownVariables.EnabledFeatureToggles, enabledFeatureToggle);
            var namespacedApiResourcesDict = new NamespacedApiResourcesDictBuilder().WithDefaults().Build();

            var yaml = @"apiVersion: apps/v1
kind: Pod
metadata:
  name: test
";
            using (CreateFile(yaml, out var filePath))
            {
                var mr = new ManifestReporter(variables, CalamariPhysicalFileSystem.GetPhysicalFileSystem(), memoryLog, namespacedApiResourcesDict);

                mr.ReportManifestApplied(filePath);

                memoryLog.ServiceMessages.First().Properties.Should().Contain(new KeyValuePair<string, string>("ns", "default"));
            }
        }

        [TestCase(nameof(FeatureToggle.KubernetesLiveObjectStatusFeatureToggle))]
        [TestCase( OctopusFeatureToggles.KnownSlugs.KubernetesObjectManifestInspection)]
        public void GivenUnknownKind_NamespaceShouldBeDefault(string enabledFeatureToggle)
        {
            var memoryLog = new InMemoryLog();
            var variables = new CalamariVariables();
            variables.Set(KnownVariables.EnabledFeatureToggles, enabledFeatureToggle);
            var namespacedApiResourcesDict = new NamespacedApiResourcesDictBuilder().WithDefaults().Build();

            var yaml = @"apiVersion: foo.bar
kind: Unknown
metadata:
  name: test
";
            using (CreateFile(yaml, out var filePath))
            {
                var mr = new ManifestReporter(variables, CalamariPhysicalFileSystem.GetPhysicalFileSystem(), memoryLog, namespacedApiResourcesDict);

                mr.ReportManifestApplied(filePath);

                memoryLog.ServiceMessages.First().Properties.Should().Contain(new KeyValuePair<string, string>("ns", "default"));
            }
        }

        [TestCase(nameof(FeatureToggle.KubernetesLiveObjectStatusFeatureToggle))]
        [TestCase( OctopusFeatureToggles.KnownSlugs.KubernetesObjectManifestInspection)]
        public void GivenExplicitNamespace_NamespaceShouldBeExplicit(string enabledFeatureToggle)
        {
            var memoryLog = new InMemoryLog();
            var variables = new CalamariVariables();
            variables.Set(KnownVariables.EnabledFeatureToggles, enabledFeatureToggle);
            var namespacedApiResourcesDict = new NamespacedApiResourcesDictBuilder().WithDefaults().Build();

            var yaml = @"apiVersion: apps/v1
kind: Pod
metadata:
  name: test
  namespace: test
";
            using (CreateFile(yaml, out var filePath))
            {
                var mr = new ManifestReporter(variables, CalamariPhysicalFileSystem.GetPhysicalFileSystem(), memoryLog, namespacedApiResourcesDict);

                mr.ReportManifestApplied(filePath);

                memoryLog.ServiceMessages.First().Properties.Should().Contain(new KeyValuePair<string, string>("ns", "test"));
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