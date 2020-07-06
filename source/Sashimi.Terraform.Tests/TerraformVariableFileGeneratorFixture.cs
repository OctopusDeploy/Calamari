using FluentAssertions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Octostache;
using Sashimi.Terraform.ActionHandler;
using Sashimi.Terraform.CloudTemplates;
using Sashimi.Tests.Shared.Server;

namespace Sashimi.Terraform.Tests
{
    [TestFixture]
    public class TerraformVariableFileGeneratorFixture
    {
        [Test]
        public void VerifyVariableFileGeneration()
        {
            var template = @"variable ""test"" {
                type = ""string""
            }

            variable ""list"" {
                type = ""list""
            }

            variable ""map"" {
                type = ""map""
            }";
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
        public void VerifyVariableFileGenerationWithSubstitution()
        {
            var template = @"variable ""test"" {
                type = ""string""
            }

            variable ""list"" {
                type = ""list""
            }

            variable ""map"" {
                type = ""map""
            }";
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
        public void VerifyVariableFileGenerationWithSubstitution2()
        {
            var template = @"variable ""test"" {
                type = ""string""
            }

            variable ""list"" {
                type = ""list""
            }

            variable ""map"" {
                type = ""map""
            }";
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