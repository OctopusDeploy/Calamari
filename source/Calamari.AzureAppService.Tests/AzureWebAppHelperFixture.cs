using Calamari.Azure;
using FluentAssertions;
using FluentAssertions.Execution;
using NUnit.Framework;
using System.Collections.Generic;

namespace Calamari.AzureAppService.Tests
{
    [TestFixture]
    public class AzureWebAppHelperFixture
    {
        [Test]
        public void GetOctopusTags_FindsMatchingTags_RegardlessOfCase()
        {
            // Arrange
            var tags = new Dictionary<string, string>
            {
                { "oCtoPus-eNviRonMenT", "taggedEnvironment" },
                { "ocTopUs-roLe", "taggedRole" },
                { "OctOpuS-ProJecT", "taggedProject" },
                { "oCtoPus-sPacE", "taggedSpace" },
                { "ocTopUs-teNanT", "taggedTenant" },
            };

            // Act
            var foundTags = AzureWebAppHelper.GetOctopusTags(tags);

            // Assert
            using (new AssertionScope())
            {
                foundTags.Environment.Should().Be("taggedEnvironment");
                foundTags.Role.Should().Be("taggedRole");
                foundTags.Project.Should().Be("taggedProject");
                foundTags.Space.Should().Be("taggedSpace");
                foundTags.Tenant.Should().Be("taggedTenant");
            }
        }
    }
}
