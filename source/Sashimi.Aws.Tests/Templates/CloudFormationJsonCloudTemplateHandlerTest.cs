using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Sashimi.Aws.CloudTemplates;
using Sashimi.Server.Contracts;

namespace Sashimi.Aws.Tests.Templates
{
    public class CloudFormationCloudJsonTemplateHandlerTest : CloudFormationCloudTemplateHandlerTestBase
    {
        [SetUp]
        public void Setup()
        {
            IFormatIdentifier formatIdentifier = Substitute.For<IFormatIdentifier>();
            formatIdentifier.IsJson(Arg.Any<string>()).Returns(true);
            formatIdentifier.IsYaml(Arg.Any<string>()).Returns(false);
            templateParser = new CloudFormationJsonCloudTemplateHandler(formatIdentifier);
        }

        [TestCase("CF", true)]
        [TestCase("CloudFormation", true)]
        [TestCase("cloudformation", true)]
        [TestCase("awscloudformation", true)]
        [TestCase("not_a_valid_id", false)]
        public void HandlesTemplateType(string id, bool expectedResult)
        {
            templateParser.CanHandleTemplate(id, "{\"greeting\": \"hello\"}").Should().Be(expectedResult);
        }

        [Test]
        public void Parse_template1_ShouldParseCorrectNumberOfParameters()
        {
            // arrange
            var template = LoadTemplate("template1.json");

            // act
            var metadata = templateParser.ParseTypes(template);

            // assert
            MetadataShouldContainCorrectNumberOfTypes(metadata, 1);
            MetadataTypeShouldContainCorrectNumberOfProperties(metadata.Types.First(), 7);
        }

        [Test]
        public void Parse_template1_ShouldCorrectlyReadAllowedValues()
        {
            // arrange
            var template = LoadTemplate("template1.json");

            // act
            var metadata = templateParser.ParseTypes(template);

            // assert
            MetadataShouldContainCorrectNumberOfTypes(metadata, 1);
            MetadataTypeShouldContainCorrectNumberOfProperties(metadata.Types.First(), 7);
            metadata.Types.First().Properties.Single(t => t.Name == "InstanceType").DisplayInfo.Options.Values.Count.Should().Be(54);
        }

        [Test]
        public void Parse_permanentInstance_ShouldCorrectlyReadAllowedValues()
        {
            // arrange
            var template = LoadTemplate("permanent-instance.json");

            // act
            var metadata = templateParser.ParseTypes(template);

            // assert
            MetadataShouldContainCorrectNumberOfTypes(metadata, 1);
            MetadataTypeShouldContainCorrectNumberOfProperties(metadata.Types.First(), 5);
            //metadata.Types.First().Properties.Single(t => t.Name == "InstanceType").DisplayInfo.Options.Values.Count.Should().Be(54);
        }

        [Test]
        public void Parse_ComplexType_ShouldCorrectlyReadTypes()
        {
            // arrange
            var template = LoadTemplate("lambda-sample.json");

            // act
            var metadata = templateParser.ParseTypes(template);

            // assert
            MetadataShouldContainCorrectNumberOfTypes(metadata, 1);
            MetadataTypeShouldContainCorrectNumberOfProperties(metadata.Types.First(), 3);
        }

        [TestCase("string_datatype.json", "string", 1)]
        [TestCase("number_datatype.json", "int", 1)]
        [TestCase("commadelimitedlist_datatype.json", "string[]", 1)]
        [TestCase("ListNumber_datatype.json", "int[]", 1)]
        [TestCase("ec2-instance.json", "string", 1)]
        [TestCase("ListGeneric_datatype.json", "string[]", 1)]
        public void Parse_DataTypes(string fileName, string expectedOutputType, int expectedParameterCount)
        {
            // arrangee
            var template = LoadTemplate(fileName);

            // act
            var metadata = templateParser.ParseTypes(template);

            // assert
            MetadataTypeShouldContainCorrectNumberOfProperties(metadata.Types[0], expectedParameterCount);
            metadata.Types.First().Properties.Single(x => x.Name == "parameter1").Type.Should().Be(expectedOutputType);
        }

        [TestCase("empty.json")]
        [TestCase("empty-noparametersnode.json")]
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
            var template = LoadTemplate("string_datatype.json");

            // act
            var model = templateParser.ParseModel(template);

            // assert
            model.Should().NotBeNull();
            var dictionary = model as IDictionary<string, object>;
            dictionary.Should().NotBeNull();
            dictionary["parameter1"].Should().Be("default");
        }

        [TestCase("empty.json")]
        [TestCase("empty-noparametersnode.json")]
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