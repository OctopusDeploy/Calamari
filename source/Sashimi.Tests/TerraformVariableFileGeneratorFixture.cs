using System;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Sashimi.Terraform.ActionHandler;
using Sashimi.Terraform.CloudTemplates;
using Sashimi.Tests.Shared.Server;

namespace Sashimi.Terraform.Tests
{
    [TestFixture]
    public class TerraformVariableFileGeneratorFixture
    {
        const string HclOneVariables = "variable \"test\" {\n\ttype = \"string\"\n}\n\nvariable \"list\" {\n\ttype = \"list\"\n}\n\nvariable \"map\" {\n\ttype = \"map\"\n}";
        const string HclTwoVariables = "variable \"test\" {\n\ttype = string\n}\n\nvariable \"list\" {\n\ttype = list\n}\n\nvariable \"map\" {\n\ttype = map\n}";

        [Test]
        [TestCase(HclOneVariables)]
        [TestCase(HclTwoVariables)]
        public void VerifyVariableFileGeneration(string template)
        {
            var metadata = new TerraformHclCloudTemplateHandler().ParseTypes(template);
            var variables = @"{""test"": ""string"", ""list"": ""[1]"", ""map"": ""{\""key\"": \""value\""}""}";
            var jsonVariables = TerraformVariableFileGenerator.ConvertStringPropsToObjects(
                                                                                           TerraformTemplateFormat.Json,
                                                                                           new TestVariableDictionary(),
                                                                                           variables,
                                                                                           metadata);
            jsonVariables.Should().Match(@"{""test"": ""string"",""list"": [1],""map"": {""key"": ""value""}}");
            // This should be valid json
            JObject.Parse(jsonVariables);
        }

        [Test]
        [TestCase(HclOneVariables)]
        [TestCase(HclTwoVariables)]
        public void VerifyVariableFileGenerationWithSubstitution(string template)
        {
            var metadata = new TerraformHclCloudTemplateHandler().ParseTypes(template);
            var variables = @"{""test"": ""#{MyVariable}"", ""list"": ""#{MyList}"", ""map"": ""#{MyMap}""}";
            var jsonVariables = TerraformVariableFileGenerator.ConvertStringPropsToObjects(
                                                                                           TerraformTemplateFormat.Json,
                                                                                           new TestVariableDictionary(),
                                                                                           variables,
                                                                                           metadata);
            jsonVariables.Should().Match(@"{""test"": ""#{MyVariable}"",""list"": #{MyList},""map"": #{MyMap}}");
        }

        /// <summary>
        /// It must be possible for the source variables to construct invalid JSON where the substitutions
        /// will eventually make up valid inputs
        /// </summary>
        [Test]
        [TestCase(HclOneVariables)]
        [TestCase(HclTwoVariables)]
        public void VerifyVariableFileGenerationWithSubstitution2(string template)
        {
            var metadata = new TerraformHclCloudTemplateHandler().ParseTypes(template);
            var variables = @"{""test"": ""#{MyVariable}"", ""list"": ""[#{MyListVariable}, 2, 3]"", ""map"": ""{\""key\"": #{MyMap}}""}";
            var jsonVariables = TerraformVariableFileGenerator.ConvertStringPropsToObjects(
                                                                                           TerraformTemplateFormat.Json,
                                                                                           new TestVariableDictionary(),
                                                                                           variables,
                                                                                           metadata);
            jsonVariables.Should().Match(@"{""test"": ""#{MyVariable}"",""list"": [#{MyListVariable}, 2, 3],""map"": {""key"": #{MyMap}}}");
        }
    }
}