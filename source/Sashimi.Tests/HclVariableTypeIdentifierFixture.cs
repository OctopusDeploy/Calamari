using FluentAssertions;
using NUnit.Framework;
using Sashimi.Terraform.CloudTemplates;

namespace Sashimi.Terraform.Tests
{
    public class HclVariableTypeIdentifierFixture
    {
        [Test]
        [TestCase("string", "string")]
        [TestCase("list", "raw_list")]
        [TestCase("list(string)", "raw_list")]
        [TestCase("tuple", "raw_list")]
        [TestCase("tuple(number)", "raw_list")]
        [TestCase("set", "raw_list")]
        [TestCase("set(any)", "raw_list")]
        [TestCase("map", "raw_map")]
        [TestCase("map({id = string, cidr_block = string})", "raw_map")]
        [TestCase("object", "raw_map")]
        [TestCase("object({id = string, cidr_block = string})", "raw_map")]
        public void CanIdentify(string type, string expected)
        {
            TerraformDataTypes.MapToType(type).Should().Be(expected);
        }
    }
}