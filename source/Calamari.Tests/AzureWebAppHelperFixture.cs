using Calamari.Azure;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Azure.Management.AppService.Fluent;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            var webApp = Substitute.For<IWebAppBasic>();
            webApp.Tags.Returns(tags);

            // Act
            var foundTags = AzureWebAppHelper.GetOctopusTags(webApp);

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
