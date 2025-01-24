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
                    { "non-string-value", 42 },
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
                    { "non-string-value", 42 },
                }
            };

            // Act
            var evaluatedTvs = HelmTemplateValueSourcesParser.KeyValuesTemplateValuesSource.FromJTokenWithEvaluation(ConvertToJObject(keyValuesTvs), variables);
            
            // Assert
            evaluatedTvs.Should().BeEquivalentTo(expectedTvs);
        }
        
        static JObject ConvertToJObject(object value) => JObject.Parse(JsonConvert.SerializeObject(value));
    }
}