﻿using Calamari.AzureAppService;
using FluentAssertions;
using FluentAssertions.Execution;
using NUnit.Framework;
using System.Collections.Generic;
using Calamari.Azure.AppServices.Azure;

namespace Calamari.AzureAppService.Tests
{
    [TestFixture]
    public class AzureWebAppTagHelperFixture
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
                { "ocTopUs-TeNanTedDeployMentMode", "Tenanted"}, 
            };

            // Act
            var foundTags = AzureWebAppTagHelper.GetOctopusTags(tags);

            // Assert
            using (new AssertionScope())
            {
                foundTags.Environment.Should().Be("taggedEnvironment");
                foundTags.Role.Should().Be("taggedRole");
                foundTags.Project.Should().Be("taggedProject");
                foundTags.Space.Should().Be("taggedSpace");
                foundTags.Tenant.Should().Be("taggedTenant");
                foundTags.TenantedDeploymentMode.Should().Be("Tenanted");
            }
        }
    }
}
