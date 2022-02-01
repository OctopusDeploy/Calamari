using System;
using System.IO;
using System.Reflection;
using FluentAssertions;
using NUnit.Framework;
using Assent;
using Sashimi.Tests.Shared.Extensions;
using Sashimi.Tests.Shared.Server;

namespace Sashimi.AzureResourceGroup.Tests
{
    [TestFixture]
    public class AzureResourceGroupActionUtilsFixture
    {
        string template = @"{
            ""$schema"": ""http://schema.management.azure.com/schemas/2015-01-01/deploymentTemplate.json#"",
            ""contentVersion"": ""1.0.0.0"",
            ""parameters"": {
                ""object1"": { ""type"": ""object"" },
                ""object2"": { ""type"": ""object"" },
                ""string"": { ""type"": ""string"" },
                ""stringWithBadlyCaseType"": { ""type"": ""String"" },
                ""aSecureObject"": { ""type"": ""secureObject"" },
                ""int"": { ""type"": ""int"" },
                ""bool"": { ""type"": ""bool"" },
                ""array"": { ""type"": ""array"" },
                ""int2"": { ""type"": ""int"" },
                ""bool2"": { ""type"": ""bool"" },
                ""array2"": { ""type"": ""array"" },
                ""complex"": { ""type"": ""string"" }
            },
            ""variables"": {
            },
            ""resources"": [
                ],
            ""outputs"": {
            }
        }";

        [Test]
        public void TemplateParameters()
        {
            var templateTypes = AzureResourceGroupActionUtils.ExtractParameterTypes(template);
            string parameterJson = @"{
   ""object1"":{
      ""value"":{
         ""name"":""John""
      }
   },
   ""object2"":{
      ""value"":""#{MyObj}""
   },
   ""string"":{
      ""value"":""Hello John""
   },
   ""stringWithBadlyCaseType"":{
      ""value"":""Hello again John""
   },
   ""aSecureObject"":{
      ""value"":{""Value"":""Foo""}
   },
   ""int"":{
      ""value"":""#{MyInt}""
   },
   ""bool"":{
      ""value"":""#{MyBool}""
   },
   ""array"":{
      ""value"":""#{MyArray}""
   },
   ""int2"":{
      ""value"":55
   },
   ""bool2"":{
      ""value"":true
   },
   ""array2"":{
      ""value"":[""foo"", ""bar""]
   },
   ""complex"":{
      ""value"":""#{Complex} Smith""
   }
}";

            var variableDictionary = new TestVariableDictionary();
            variableDictionary.Set("MyObj", @"{ ""name"": ""Tim"" }");
            variableDictionary.Set("MyInt", "40");
            variableDictionary.Set("MyBool", Boolean.TrueString);
            variableDictionary.Set("MyArray", @"[ { ""points"": 67 }, { ""points"": 77 } ]");
            variableDictionary.Set("Complex", @"Hello #{if ATruthyVariable}Oliver#{else}Ella#{/if}");
            variableDictionary.Set("ATruthyVariable", Boolean.TrueString);
            var result = AzureResourceGroupActionUtils.TemplateParameters(parameterJson, templateTypes, variableDictionary);

            this.Assent(result, new Configuration().UsingNamer(new SashimiNamer()));
        }

        class SashimiNamer : INamer
        {
           public string GetName(TestMetadata metadata)
           {
              var directoryName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

              return Path.Combine(directoryName, $"{metadata.TestFixture.GetType().Name}.{metadata.TestName}");
           }
        }

        [Test]
        public void ExtractParameterTypes()
        {
            var result = AzureResourceGroupActionUtils.ExtractParameterTypes(template);

            result.Count.Should().Be(12);
            result.Should().ContainKeys("object1", "object2", "string", "stringWithBadlyCaseType", "aSecureObject", "int", "bool", "array", "int2", "bool2", "array2", "complex");
            result["object1"].Should().Be("object");
            result["object2"].Should().Be("object");
            result["string"].Should().Be("string");
            result["stringWithBadlyCaseType"].Should().Be("String");
            result["aSecureObject"].Should().Be("secureObject");
            result["int"].Should().Be("int");
            result["bool"].Should().Be("bool");
            result["array"].Should().Be("array");
        }
    }
}