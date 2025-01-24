using System;
using System.Collections.Generic;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.Helm;
using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.Helm
{
    [TestFixture]
    public class HelmTemplateValueSourcesFixture
    {
        const string ExampleJson = @"{
  ""name"": ""Test User"",
  ""id"": 1
}";

        const string ExampleYaml = @"replicas: 3
config:
  'min-replicas-to-write': 1
  ""string-quoted-key"": ""string-quoted-value""
  numbers:
   - 42
   - 3
";

        public static IEnumerable<TestCaseData> InlineYamlTestCases
        {
            get
            {
                yield return new TestCaseData("#{MyExampleJson}",
                                              new CalamariVariables
                                              {
                                                  ["MyExampleJson"] = ExampleJson
                                              },
                                              ExampleJson);

                yield return new TestCaseData("#{Service.HelmYaml}",
                                              new CalamariVariables
                                              {
                                                  ["Service.HelmYaml"] = ExampleYaml
                                              },
                                              ExampleYaml);

                yield return new TestCaseData("colors: #{JsonArray}",
                                              new CalamariVariables
                                              {
                                                  ["JsonArray"] = @"[""red"",""green"",""blue""]"
                                              },
                                              @"colors: [""red"",""green"",""blue""]");
            }
        }

        [Test]
        public void FromJTokenWithEvaluation_KeyValuesTvs_VariablesAreEvaluated()
        {
            // Arrange
            var keyValuesTvs = new HelmTemplateValueSourcesParser.KeyValuesTemplateValuesSource
            {
                Value = new Dictionary<string, object>
                {
                    { "#{MyKey1}", "#{MyValue1}" },
                    { "#{MyKey2}", "environments/#{MyValue2}" },
                    { "non-string-value", 42 },
                    { "jsonBlob", "#{JsonBlob}" },
                }
            };
            var variables = new CalamariVariables
            {
                ["MyKey1"] = "name",
                ["MyValue1"] = "octopus",
                ["MyKey2"] = "env",
                ["MyValue2"] = "#{Environment}",
                ["Environment"] = "dev",
                ["JsonBlob"] = ExampleJson
            };

            var expectedTvs = new HelmTemplateValueSourcesParser.KeyValuesTemplateValuesSource
            {
                Value = new Dictionary<string, object>
                {
                    { "name", "octopus" },
                    { "env", "environments/dev" },
                    { "non-string-value", 42 },
                    { "jsonBlob", ExampleJson },
                }
            };

            // Act
            var evaluatedTvs = HelmTemplateValueSourcesParser.KeyValuesTemplateValuesSource.FromJTokenWithEvaluation(ConvertToJObject(keyValuesTvs), variables);

            // Assert
            evaluatedTvs.Should().BeEquivalentTo(expectedTvs);
        }

        [Test]
        [TestCaseSource(nameof(InlineYamlTestCases))]
        public void FromJTokenWithEvaluation_InlineYamlTvs_VariablesAreEvaluated(string value, IVariables variables, string expectedValue)
        {
            // Arrange
            var inlineYamlTvs = new HelmTemplateValueSourcesParser.InlineYamlTemplateValuesSource
            {
                Value = value
            };

            var expectedTvs = new HelmTemplateValueSourcesParser.InlineYamlTemplateValuesSource
            {
                Value = expectedValue
            };

            // Act
            var evaluatedTvs = HelmTemplateValueSourcesParser.InlineYamlTemplateValuesSource.FromJTokenWithEvaluation(ConvertToJObject(inlineYamlTvs), variables);

            // Assert
            evaluatedTvs.Should().BeEquivalentTo(expectedTvs);
        }

        [Test]
        public void FromJTokenWithEvaluation_ChartTvs_VariablesAreEvaluated()
        {
            // Arrange
            var chartTvs = new HelmTemplateValueSourcesParser.ChartTemplateValuesSource
            {
                ValuesFilePaths = "values/#{Octopus.Environment.Name | ToLower}.yaml"
            };
            var variables = new CalamariVariables
            {
                ["Octopus.Environment.Name"] = "Dev"
            };

            var expectedTvs = new HelmTemplateValueSourcesParser.ChartTemplateValuesSource
            {
                ValuesFilePaths = "values/dev.yaml"
            };

            // Act
            var evaluatedTvs = HelmTemplateValueSourcesParser.ChartTemplateValuesSource.FromJTokenWithEvaluation(ConvertToJObject(chartTvs), variables);

            // Assert
            evaluatedTvs.Should().BeEquivalentTo(expectedTvs);
        }

        [Test]
        public void FromJTokenWithEvaluation_PackageTvs_VariablesAreEvaluated()
        {
            // Arrange
            var packageTvs = new HelmTemplateValueSourcesParser.PackageTemplateValuesSource
            {
                PackageId = "#{Package.Id}",
                PackageName = "#{Package.Name}",
                ValuesFilePaths = "values/#{Octopus.Environment.Name | ToLower}.yaml"
            };
            var variables = new CalamariVariables
            {
                ["Package.Id"] = "0a5c0b5d70e6",
                ["Package.Name"] = "my-helm-package",
                ["Octopus.Environment.Name"] = "Dev"
            };

            var expectedTvs = new HelmTemplateValueSourcesParser.PackageTemplateValuesSource
            {
                PackageId = "0a5c0b5d70e6",
                PackageName = "my-helm-package",
                ValuesFilePaths = "values/dev.yaml"
            };

            // Act
            var evaluatedTvs = HelmTemplateValueSourcesParser.PackageTemplateValuesSource.FromJTokenWithEvaluation(ConvertToJObject(packageTvs), variables);

            // Assert
            evaluatedTvs.Should().BeEquivalentTo(expectedTvs);
        }

        [Test]
        public void FromJTokenWithEvaluation_GitRepositoryTvs_VariablesAreEvaluated()
        {
            // Arrange
            var gitRepoTvs = new HelmTemplateValueSourcesParser.GitRepositoryTemplateValuesSource
            {
                GitDependencyName = "#{MyGitDependency}",
                ValuesFilePaths = "values/#{Octopus.Environment.Name | ToLower}.yaml"
            };
            var variables = new CalamariVariables
            {
                ["MyGitDependency"] = "helm-git-repository",
                ["Octopus.Environment.Name"] = "Dev"
            };

            var expectedTvs = new HelmTemplateValueSourcesParser.GitRepositoryTemplateValuesSource
            {
                GitDependencyName = "helm-git-repository",
                ValuesFilePaths = "values/dev.yaml"
            };

            // Act
            var evaluatedTvs = HelmTemplateValueSourcesParser.GitRepositoryTemplateValuesSource.FromJTokenWithEvaluation(ConvertToJObject(gitRepoTvs), variables);

            // Assert
            evaluatedTvs.Should().BeEquivalentTo(expectedTvs);
        }

        static JObject ConvertToJObject(object value)
        {
            return JObject.Parse(JsonConvert.SerializeObject(value));
        }
    }
}