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
using Calamari.Tests.KubernetesFixtures.Builders;
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
            var apiResourceScopeLookup = new ApiResourcesScopeLookupBuilder().WithDefaults().Build();

            var yaml = @"foo: bar";
            using (CreateFile(yaml, out var filePath))
            {
                var mr = new ManifestReporter(variables, CalamariPhysicalFileSystem.GetPhysicalFileSystem(), memoryLog, apiResourceScopeLookup);

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

            var apiResourceScopeLookup = new ApiResourcesScopeLookupBuilder().WithDefaults().Build();

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
                var mr = new ManifestReporter(variables, CalamariPhysicalFileSystem.GetPhysicalFileSystem(), memoryLog, apiResourceScopeLookup);

                mr.ReportManifestFileApplied(filePath);

                var expected = ServiceMessage.Create(SpecialVariables.ServiceMessages.ManifestApplied.Name, ("ns", "default"), ("manifest", yaml));
                memoryLog.ServiceMessages.Should().BeEquivalentTo(new List<ServiceMessage> { expected });
            }
        }

        [TestCase(nameof(FeatureToggle.KubernetesLiveObjectStatusFeatureToggle))]
        [TestCase(OctopusFeatureToggles.KnownSlugs.KubernetesObjectManifestInspection)]
        public void GivenInValidManifest_ShouldNotPostServiceMessage(string enabledFeatureToggle)
        {
            var memoryLog = new InMemoryLog();
            var variables = new CalamariVariables();
            variables.Set(KnownVariables.EnabledFeatureToggles, enabledFeatureToggle);

            var apiResourceScopeLookup = new ApiResourcesScopeLookupBuilder().WithDefaults().Build();

            var yaml = @"text - Bar";
            using (CreateFile(yaml, out var filePath))
            {
                var mr = new ManifestReporter(variables, CalamariPhysicalFileSystem.GetPhysicalFileSystem(), memoryLog, apiResourceScopeLookup);

                mr.ReportManifestFileApplied(filePath);

                memoryLog.ServiceMessages.Should().BeEmpty();
            }
        }

        [TestCase(nameof(FeatureToggle.KubernetesLiveObjectStatusFeatureToggle))]
        [TestCase(OctopusFeatureToggles.KnownSlugs.KubernetesObjectManifestInspection)]
        public void GivenNamespaceInManifest_ShouldReportManifestNamespace(string enabledFeatureToggle)
        {
            var memoryLog = new InMemoryLog();
            var variables = new CalamariVariables();
            variables.Set(KnownVariables.EnabledFeatureToggles, enabledFeatureToggle);

            var apiResourceScopeLookup = new ApiResourcesScopeLookupBuilder().WithDefaults().Build();

            var yaml = @"metadata:
  name: game-demo
  namespace: XXX";
            using (CreateFile(yaml, out var filePath))
            {
                var variableNs = Some.String();
                variables.Set(SpecialVariables.Namespace, variableNs);
                var mr = new ManifestReporter(variables, CalamariPhysicalFileSystem.GetPhysicalFileSystem(), memoryLog, apiResourceScopeLookup);

                mr.ReportManifestFileApplied(filePath);

                memoryLog.ServiceMessages.First().Properties.Should().Contain(new KeyValuePair<string, string>("ns", "XXX"));
            }
        }

        [TestCase(nameof(FeatureToggle.KubernetesLiveObjectStatusFeatureToggle))]
        [TestCase(OctopusFeatureToggles.KnownSlugs.KubernetesObjectManifestInspection)]
        public void GivenNamespaceNotInManifest_ShouldReportVariableNamespace(string enabledFeatureToggle)
        {
            var memoryLog = new InMemoryLog();
            var variables = new CalamariVariables();
            variables.Set(KnownVariables.EnabledFeatureToggles, enabledFeatureToggle);

            var apiResourceScopeLookup = new ApiResourcesScopeLookupBuilder().WithDefaults().Build();

            var yaml = @"foo: bar";
            using (CreateFile(yaml, out var filePath))
            {
                var variableNs = Some.String();
                variables.Set(SpecialVariables.Namespace, variableNs);
                var mr = new ManifestReporter(variables, CalamariPhysicalFileSystem.GetPhysicalFileSystem(), memoryLog, apiResourceScopeLookup);

                mr.ReportManifestFileApplied(filePath);

                memoryLog.ServiceMessages.First().Properties.Should().Contain(new KeyValuePair<string, string>("ns", variableNs));
            }
        }
        
        [TestCase(nameof(FeatureToggle.KubernetesLiveObjectStatusFeatureToggle))]
        [TestCase(OctopusFeatureToggles.KnownSlugs.KubernetesObjectManifestInspection)]
        public void GivenNamespaceNotInManifest_ShouldUseHelmVariableNamespace(string enabledFeatureToggle)
        {
            var memoryLog = new InMemoryLog();
            var variables = new CalamariVariables();
            variables.Set(KnownVariables.EnabledFeatureToggles, enabledFeatureToggle);

            var apiResourceScopeLookup = new ApiResourcesScopeLookupBuilder().WithDefaults().Build();

            var yaml = @"foo: bar";
            using (CreateFile(yaml, out var filePath))
            {
                var helmNs = Some.String();
                var variableNs = Some.String();
                variables.Set(SpecialVariables.Helm.Namespace, helmNs);
                variables.Set(SpecialVariables.Namespace, variableNs);
                var mr = new ManifestReporter(variables, CalamariPhysicalFileSystem.GetPhysicalFileSystem(), memoryLog, apiResourceScopeLookup);

                mr.ReportManifestFileApplied(filePath);

                memoryLog.ServiceMessages.First().Properties.Should().Contain(new KeyValuePair<string, string>("ns", helmNs));
            }
        }

        [TestCase(nameof(FeatureToggle.KubernetesLiveObjectStatusFeatureToggle))]
        [TestCase(OctopusFeatureToggles.KnownSlugs.KubernetesObjectManifestInspection)]
        public void GiveNoNamespaces_ShouldDefaultNamespace(string enabledFeatureToggle)
        {
            var memoryLog = new InMemoryLog();
            var variables = new CalamariVariables();
            variables.Set(KnownVariables.EnabledFeatureToggles, enabledFeatureToggle);

            var apiResourceScopeLookup = new ApiResourcesScopeLookupBuilder().WithDefaults().Build();

            var yaml = @"foo: bar";
            using (CreateFile(yaml, out var filePath))
            {
                var mr = new ManifestReporter(variables, CalamariPhysicalFileSystem.GetPhysicalFileSystem(), memoryLog, apiResourceScopeLookup);

                mr.ReportManifestFileApplied(filePath);

                memoryLog.ServiceMessages.First().Properties.Should().Contain(new KeyValuePair<string, string>("ns", "default"));
            }
        }

        [TestCase(nameof(FeatureToggle.KubernetesLiveObjectStatusFeatureToggle))]
        [TestCase(OctopusFeatureToggles.KnownSlugs.KubernetesObjectManifestInspection)]
        public void GivenValidYamlString_ShouldPostSingleServiceMessage(string enabledFeatureToggle)
        {
            var memoryLog = new InMemoryLog();
            var variables = new CalamariVariables();
            variables.Set(KnownVariables.EnabledFeatureToggles, enabledFeatureToggle);

            var apiResourceScopeLookup = new ApiResourcesScopeLookupBuilder().WithDefaults().Build();

            const string yaml = "foo: bar";
            var expectedYaml = $"foo: bar{Environment.NewLine}";
            var mr = new ManifestReporter(variables, CalamariPhysicalFileSystem.GetPhysicalFileSystem(), memoryLog, apiResourceScopeLookup);

            mr.ReportManifestApplied(yaml);

            var expected = ServiceMessage.Create(SpecialVariables.ServiceMessages.ManifestApplied.Name, ("ns", "default"), ("manifest", expectedYaml));
            memoryLog.ServiceMessages.Should().BeEquivalentTo(new List<ServiceMessage> { expected });
        }

        [TestCase(nameof(FeatureToggle.KubernetesLiveObjectStatusFeatureToggle))]
        [TestCase(OctopusFeatureToggles.KnownSlugs.KubernetesObjectManifestInspection)]
        public void GivenGlobalKind_NamespaceShouldBeNull(string enabledFeatureToggle)
        {
            var memoryLog = new InMemoryLog();
            var variables = new CalamariVariables();
            variables.Set(KnownVariables.EnabledFeatureToggles, enabledFeatureToggle);
            var apiResourceScopeLookup = new ApiResourcesScopeLookupBuilder().WithDefaults().Build();

            const string yaml = @"apiVersion: v1
kind: Namespace
metadata:
  name: test
";
            var mr = new ManifestReporter(variables, CalamariPhysicalFileSystem.GetPhysicalFileSystem(), memoryLog, apiResourceScopeLookup);

            mr.ReportManifestApplied(yaml);

            memoryLog.ServiceMessages.First().Properties.Should().Contain(new KeyValuePair<string, string>("ns", null));
        }

        [TestCase(nameof(FeatureToggle.KubernetesLiveObjectStatusFeatureToggle))]
        [TestCase(OctopusFeatureToggles.KnownSlugs.KubernetesObjectManifestInspection)]
        public void GivenGlobalKind_ButManifestHasNamespace_NamespaceShouldBeNull(string enabledFeatureToggle)
        {
            var memoryLog = new InMemoryLog();
            var variables = new CalamariVariables();
            variables.Set(KnownVariables.EnabledFeatureToggles, enabledFeatureToggle);
            var apiResourceScopeLookup = new ApiResourcesScopeLookupBuilder().WithDefaults().Build();

            const string yaml = @"apiVersion: v1
kind: Namespace
metadata:
  name: test
  namspace: my-cool-namespace";
            var mr = new ManifestReporter(variables, CalamariPhysicalFileSystem.GetPhysicalFileSystem(), memoryLog, apiResourceScopeLookup);

            mr.ReportManifestApplied(yaml);

            memoryLog.ServiceMessages.First().Properties.Should().Contain(new KeyValuePair<string, string>("ns", null));
        }

        [TestCase(nameof(FeatureToggle.KubernetesLiveObjectStatusFeatureToggle))]
        [TestCase(OctopusFeatureToggles.KnownSlugs.KubernetesObjectManifestInspection)]
        public void GivenNamespacedKind_NamespaceShouldBeDefault(string enabledFeatureToggle)
        {
            var memoryLog = new InMemoryLog();
            var variables = new CalamariVariables();
            variables.Set(KnownVariables.EnabledFeatureToggles, enabledFeatureToggle);
            var apiResourceScopeLookup = new ApiResourcesScopeLookupBuilder().WithDefaults().Build();

            const string yaml = @"apiVersion: apps/v1
kind: Pod
metadata:
  name: test
";

            var mr = new ManifestReporter(variables, CalamariPhysicalFileSystem.GetPhysicalFileSystem(), memoryLog, apiResourceScopeLookup);

            mr.ReportManifestApplied(yaml);

            memoryLog.ServiceMessages.First().Properties.Should().Contain(new KeyValuePair<string, string>("ns", "default"));
        }

        [TestCase(nameof(FeatureToggle.KubernetesLiveObjectStatusFeatureToggle))]
        [TestCase(OctopusFeatureToggles.KnownSlugs.KubernetesObjectManifestInspection)]
        public void GivenUnknownKind_NamespaceShouldBeDefault(string enabledFeatureToggle)
        {
            var memoryLog = new InMemoryLog();
            var variables = new CalamariVariables();
            variables.Set(KnownVariables.EnabledFeatureToggles, enabledFeatureToggle);
            var apiResourceScopeLookup = new ApiResourcesScopeLookupBuilder().WithDefaults().Build();

            const string yaml = @"apiVersion: foo.bar
kind: Unknown
metadata:
  name: test
";

            var mr = new ManifestReporter(variables, CalamariPhysicalFileSystem.GetPhysicalFileSystem(), memoryLog, apiResourceScopeLookup);

            mr.ReportManifestApplied(yaml);

            memoryLog.ServiceMessages.First().Properties.Should().Contain(new KeyValuePair<string, string>("ns", "default"));
        }

        [TestCase(nameof(FeatureToggle.KubernetesLiveObjectStatusFeatureToggle))]
        [TestCase(OctopusFeatureToggles.KnownSlugs.KubernetesObjectManifestInspection)]
        public void GivenExplicitNamespace_NamespaceShouldBeExplicit(string enabledFeatureToggle)
        {
            var memoryLog = new InMemoryLog();
            var variables = new CalamariVariables();
            variables.Set(KnownVariables.EnabledFeatureToggles, enabledFeatureToggle);
            var apiResourceScopeLookup = new ApiResourcesScopeLookupBuilder().WithDefaults().Build();

            const string yaml = @"apiVersion: apps/v1
kind: Pod
metadata:
  name: test
  namespace: test
";

            var mr = new ManifestReporter(variables, CalamariPhysicalFileSystem.GetPhysicalFileSystem(), memoryLog, apiResourceScopeLookup);

            mr.ReportManifestApplied(yaml);

            memoryLog.ServiceMessages.First().Properties.Should().Contain(new KeyValuePair<string, string>("ns", "test"));
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