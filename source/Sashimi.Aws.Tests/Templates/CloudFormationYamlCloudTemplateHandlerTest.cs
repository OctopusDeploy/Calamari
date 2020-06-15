using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Sashimi.Aws.CloudTemplates;
using Sashimi.Server.Contracts;

namespace Sashimi.Aws.Tests.Templates
{
    public class CloudFormationCloudYamlTemplateHandlerTest : CloudFormationCloudTemplateHandlerTestBase
    {
        [SetUp]
        public void Setup()
        {
            IFormatIdentifier formatIdentifier = Substitute.For<IFormatIdentifier>();
            formatIdentifier.IsJson(Arg.Any<string>()).Returns(false);
            formatIdentifier.IsYaml(Arg.Any<string>()).Returns(true);
            templateParser = new CloudFormationYamlCloudTemplateHandler(formatIdentifier);
        }

        [TestCase("cf", true)]
        [TestCase("CloudFormation", true)]
        [TestCase("cloudformation", true)]
        [TestCase("awscloudformation", true)]
        [TestCase("not_a_valid_id", false)]
        public void HandlesTemplateType(string id, bool expectedResult)
        {
            templateParser.CanHandleTemplate(id, "greeting: hello").Should().Be(expectedResult);
        }


        [Test]
        public void Parse_template1_ShouldParseCorrectNumberOfParameters()
        {
            // arrange
            var template = LoadTemplate("template1.yaml");

            // act
            var metadata = templateParser.ParseTypes(template);

            // assert
            MetadataShouldContainCorrectNumberOfTypes(metadata, 1);
            MetadataTypeShouldContainCorrectNumberOfProperties(metadata.Types[0], 7);
        }

        [TestCase("string_datatype.yaml", "string", 1)]
        [TestCase("number_datatype.yaml", "int", 1)]
        [TestCase("commadelimitedlist_datatype.yaml", "string[]", 1)]
        [TestCase("ListNumber_datatype.yaml", "int[]", 1)]
        [TestCase("ListGeneric_datatype.yaml", "string[]", 1)]
        public void Parse_DataTypes(string fileName, string expectedOutputType, int expectedParameterCount)
        {
            // arrangee
            var template = LoadTemplate(fileName);

            // act
            var metadata = templateParser.ParseTypes(template);

            // assert
            MetadataTypeShouldContainCorrectNumberOfProperties(metadata.Types[0], expectedParameterCount);
            metadata.Types.First().Properties.First().Type.Should().Be(expectedOutputType);
        }

        [TestCase("empty.yaml")]
        [TestCase("empty-noparametersnode.yaml")]
        public void Parse_NoParameters_ShouldReturnEmptyTypeList(string filename)
        {
            // arrange
            var template = LoadTemplate(filename);

            // act
            var metadata = templateParser.ParseTypes(template);

            // assert
            metadata.Should().NotBeNull();
            metadata.Types.Should().NotBeEmpty();
            metadata.Types.First().Properties.Should().BeEmpty();
        }

        [Test]
        public void ParseModel_SingleParameterWithDefault_ShouldReturnObjectWithDefaultValueSet()
        {
            // arrange
            var template = LoadTemplate("string_datatype.yaml");

            // act
            var model = templateParser.ParseModel(template);

            // assert
            model.Should().NotBeNull();
            var dictionary = model as IDictionary<string, object>;
            dictionary.Should().NotBeNull();
            dictionary["DBName"].Should().Be("wordpressdb");
        }

        [TestCase("empty.yaml")]
        [TestCase("empty-noparametersnode.yaml")]
        public void ParseModel_NoParameter_ShouldReturnEmptyObject(string filename)
        {
            // arrange
            var template = LoadTemplate(filename);

            // act
            var model = templateParser.ParseModel(template);

            // assert
            model.Should().NotBeNull();
        }

    }
}