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
                    { "non-string-value", 42 }
                }
            };
            var variables = new CalamariVariables
            {
                ["MyKey1"] = "name",
                ["MyValue1"] = "octopus",
                ["MyKey2"] = "env",
                ["MyValue2"] = "#{Environment}",
                ["Environment"] = "dev"
            };

            var expectedTvs = new HelmTemplateValueSourcesParser.KeyValuesTemplateValuesSource
            {
                Value = new Dictionary<string, object>
                {
                    { "name", "octopus" },
                    { "env", "environments/dev" },
                    { "non-string-value", 42 }
                }
            };

            // Act
            var evaluatedTvs = HelmTemplateValueSourcesParser.KeyValuesTemplateValuesSource.FromJTokenWithEvaluation(ConvertToJObject(keyValuesTvs), variables);

            // Assert
            evaluatedTvs.Should().BeEquivalentTo(expectedTvs);
        }

        [Test]
        public void FromJTokenWithEvaluation_InlineYamlTvs_VariablesAreEvaluated()
        {
            // Arrange
            var keyValuesTvs = new HelmTemplateValueSourcesParser.InlineYamlTemplateValuesSource
            {
                Value = "colors: #{JsonArray}"
            };
            var variables = new CalamariVariables
            {
                ["JsonArray"] = @"[""red"",""green"",""blue""]"
            };

            var expectedTvs = new HelmTemplateValueSourcesParser.InlineYamlTemplateValuesSource
            {
                Value = @"colors: [""red"",""green"",""blue""]"
            };

            // Act
            var evaluatedTvs = HelmTemplateValueSourcesParser.InlineYamlTemplateValuesSource.FromJTokenWithEvaluation(ConvertToJObject(keyValuesTvs), variables);

            // Assert
            evaluatedTvs.Should().BeEquivalentTo(expectedTvs);
        }
        
        [Test]
        public void FromJTokenWithEvaluation_ChartTvs_VariablesAreEvaluated()
        {
            // Arrange
            var keyValuesTvs = new HelmTemplateValueSourcesParser.ChartTemplateValuesSource
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
            var evaluatedTvs = HelmTemplateValueSourcesParser.ChartTemplateValuesSource.FromJTokenWithEvaluation(ConvertToJObject(keyValuesTvs), variables);

            // Assert
            evaluatedTvs.Should().BeEquivalentTo(expectedTvs);
        }

        static JObject ConvertToJObject(object value)
        {
            return JObject.Parse(JsonConvert.SerializeObject(value));
        }
    }
}