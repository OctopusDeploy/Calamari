using System;
using System.IO;
using System.Linq;
using Calamari.Kubernetes;
using Calamari.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;
using YamlDotNet.RepresentationModel;

namespace Calamari.Tests.KubernetesFixtures
{
    [TestFixture]
    public class ManifestDataMaskerTests
    {
        [TestCaseSource(nameof(MaskSensitiveDataTestData))]
        public void MaskSensitiveData_ShouldMaskExpectedTasks(string sourceYaml, string expectedYaml)
        {
            //Arrange
            YamlMappingNode node;
            using (var reader = new StringReader(sourceYaml))
            {
                var stream = new YamlStream();
                stream.Load(reader);

                var doc = stream.Documents.First();
                node = (YamlMappingNode)doc.RootNode;
            }

            expectedYaml = expectedYaml.ReplaceLineEndings();

            //Act
            ManifestDataMasker.MaskSensitiveData(node);

            //Assert
            using (var writer = new StringWriter())
            {
                var writeStream = new YamlStream(new YamlDocument(node));
                writeStream.Save(writer);

                var outputYaml = writer.ToString()
                                       //The yaml stream adds a document separator (...) to the end of the yaml (even for a single document), so strip it as we don't care for the test assertion
                                       .TrimEnd('\r', '\n', '.')
                                       .ReplaceLineEndings();

                outputYaml.Should().Be(expectedYaml);
            }
        }

        
        public static object[] MaskSensitiveDataTestData = {
            new TestCaseData(@"apiVersion: v1
kind: Namespace
metadata:
  name: my-cool-namespace",
                             @"apiVersion: v1
kind: Namespace
metadata:
  name: my-cool-namespace"
                            )
                .SetName("Non-secret manifest has no masking applied"),
            new TestCaseData(@"apiVersion: v1
kind: ConfigMap
metadata:
  name: game-demo
data:
  player_initial_lives: ""3""
  ui_properties_file_name: ""user-interface.properties""",
                             @"apiVersion: v1
kind: ConfigMap
metadata:
  name: game-demo
data:
  player_initial_lives: ""3""
  ui_properties_file_name: ""user-interface.properties"""
                            )
                .SetName("ConfigMap manifest has no masking applied"),
            new TestCaseData(@"apiVersion: v1
kind: Secret
metadata:
  name: dotfile-secret
data:
  .secret-file: dmFsdWUtMg0KDQo=
  another-secret: ""this-is-not-a-base64-value""",
                             @"apiVersion: v1
kind: Secret
metadata:
  name: dotfile-secret
data:
  .secret-file: ""<redacted-HTIOeIP7rD4Wa4OGFOrZDOgzs/Ns7RxxQUSMW5AM9zM=>""
  another-secret: ""<redacted-LjxWWuTodgQ0Z95zOPbBWpkk9icLpHtGBa9sm2Z/U4k=>"""
                            )
                .SetName("Secret manifest has data values masked")
        };
    }
}