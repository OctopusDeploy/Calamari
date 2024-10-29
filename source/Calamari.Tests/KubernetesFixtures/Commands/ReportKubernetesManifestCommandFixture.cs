using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.ServiceMessages;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;
using Calamari.Kubernetes.Commands;
using Calamari.Testing.Helpers;
using Calamari.Tests.Fixtures.Integration.FileSystem;
using Calamari.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.Commands
{
    [TestFixture]
    public class ReportKubernetesManifestCommandFixture
    {
        [TestCase(nameof(FeatureToggle.KubernetesLiveObjectStatusFeatureToggle))]
        [TestCase( OctopusFeatureToggles.KnownSlugs.KubernetesObjectManifestInspection)]
        public void DoAThing(string enabledFeatureToggle)
        {
            // Arrange
            var fs = new TestCalamariPhysicalFileSystem();
            var log = new InMemoryLog();
            var variables = new CalamariVariables();
            variables.Set(KnownVariables.EnabledFeatureToggles, enabledFeatureToggle);

            //write the text to a temporary file
            var tempFileStream = fs.CreateTemporaryFile("yaml", out var filePath);
            using (var writer = new StreamWriter(tempFileStream))
            {
                writer.Write(@"apiVersion: v1
kind: Secret
metadata:
  name: dotfile-secret
  namespace: ""my-cool-namespace""
data:
  .secret-file: dmFsdWUtMg0KDQo=
  another-secret: ""this-is-not-a-base64-value""");
            }
            
            var expectedYaml =  @"apiVersion: v1
kind: Secret
metadata:
  name: dotfile-secret
  namespace: ""my-cool-namespace""
data:
  .secret-file: <redacted-HTIOeIP7rD4Wa4OGFOrZDOgzs/Ns7RxxQUSMW5AM9zM=>
  another-secret: ""<redacted-LjxWWuTodgQ0Z95zOPbBWpkk9icLpHtGBa9sm2Z/U4k=>""
".ReplaceLineEndings();
            
            var command = CreateCommand(log,fs, variables);

            // Act
            var result = command.Execute(new []{ $"-path={filePath}"});
            
            // Assert
            var expected = ServiceMessage.Create(SpecialVariables.ServiceMessageNames.ManifestApplied.Name, ("ns", "my-cool-namespace"), ("manifest", expectedYaml));
            log.ServiceMessages.Should().BeEquivalentTo(new List<ServiceMessage> { expected });
            
            result.Should().Be(0);
        }

        ReportKubernetesManifestCommand CreateCommand(ILog log, ICalamariFileSystem fs, IVariables variables)
        {
            var manifestReporter = new ManifestReporter(variables, fs, log);
            return new ReportKubernetesManifestCommand(log, fs, manifestReporter);
        }
    }
}