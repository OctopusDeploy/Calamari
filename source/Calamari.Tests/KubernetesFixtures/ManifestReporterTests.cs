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
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures
{
    [TestFixture]
    public class ManifestReporterTests
    {
        // [Test]
        // public void GivenDisabledFeatureToggle_ShouldNotPostServiceMessage()
        // {
        //     var memoryLog = new InMemoryLog();
        //     var variables = new CalamariVariables();
        //
        //     var yaml = @"foo: bar";
        //     using (CreateFile(yaml, out var filePath))
        //     {
        //         var mr = new ManifestReporter(variables, CalamariPhysicalFileSystem.GetPhysicalFileSystem(), memoryLog);
        //
        //         mr.ReportManifestApplied(filePath, "default");
        //
        //         memoryLog.ServiceMessages.Should().BeEmpty();
        //     }
        // }
        
        [TestCase(nameof(FeatureToggle.KubernetesLiveObjectStatusFeatureToggle))]
        [TestCase( OctopusFeatureToggles.KnownSlugs.KubernetesObjectManifestInspection)]
        public void GivenValidYaml_ShouldPostSingleServiceMessage(string enabledFeatureToggle)
        {
            var memoryLog = new InMemoryLog();
            var variables = new CalamariVariables();
            variables.Set(KnownVariables.EnabledFeatureToggles, enabledFeatureToggle);
            
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
                var mr = new ManifestReporter(variables, CalamariPhysicalFileSystem.GetPhysicalFileSystem(), memoryLog);

                mr.ReportManifestApplied(filePath, "default");

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
                var mr = new ManifestReporter(variables, CalamariPhysicalFileSystem.GetPhysicalFileSystem(), memoryLog);

                mr.ReportManifestApplied(filePath, "default");

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

                mr.ReportManifestApplied(filePath, "default");

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

                mr.ReportManifestApplied(filePath, variableNs);

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

                mr.ReportManifestApplied(filePath, "default");

                memoryLog.ServiceMessages.First().Properties.Should().Contain(new KeyValuePair<string, string>("ns", "default"));
            }
        }
        
        [TestCase(nameof(FeatureToggle.KubernetesLiveObjectStatusFeatureToggle))]
        [TestCase( OctopusFeatureToggles.KnownSlugs.KubernetesObjectManifestInspection)]
        public void SecretManifest_ShouldRedactDataValues(string enabledFeatureToggle)
        {
            var memoryLog = new InMemoryLog();
            var variables = new CalamariVariables();
            variables.Set(KnownVariables.EnabledFeatureToggles, enabledFeatureToggle);
            
            const string yaml = @"apiVersion: v1
kind: Secret
metadata:
  name: dotfile-secret
data:
  .secret-file: dmFsdWUtMg0KDQo=
  another-secret: ""this-is-not-a-base64-value""";
            var expectedYaml =  @"apiVersion: v1
kind: Secret
metadata:
  name: dotfile-secret
data:
  .secret-file: <redacted-HTIOeIP7rD4Wa4OGFOrZDOgzs/Ns7RxxQUSMW5AM9zM=>
  another-secret: ""<redacted-LjxWWuTodgQ0Z95zOPbBWpkk9icLpHtGBa9sm2Z/U4k=>""
".ReplaceLineEndings();
            
            using (CreateFile(yaml, out var filePath))
            {
                var mr = new ManifestReporter(variables, CalamariPhysicalFileSystem.GetPhysicalFileSystem(), memoryLog);

                mr.ReportManifestApplied(filePath, "default");

                var expected = ServiceMessage.Create(SpecialVariables.ServiceMessageNames.ManifestApplied.Name, ("ns", "default"), ("manifest", expectedYaml));
                memoryLog.ServiceMessages.Should().BeEquivalentTo(new List<ServiceMessage> { expected });
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