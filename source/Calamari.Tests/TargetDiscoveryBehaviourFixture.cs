using Calamari.Azure;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Variables;
using Calamari.Tests.Shared.Helpers;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Calamari.AzureAppService.Tests
{
    [TestFixture]
    public class TargetDiscoveryBehaviourFixture
    {
        [Test]
        public async Task Exectute_FindsWebApp_WhenOneExistsWithCorrectTags()
        {
            // Arrange
            var variables = new CalamariVariables();
            var context = new RunningDeployment(variables);
            this.CreateVariables(context);
            var log = new InMemoryLog();
            var sut = new TargetDiscoveryBehaviour(log);

            // Act
            await sut.Execute(context);

            // Assert
            log.StandardOut.Should().NotBeEmpty();
        }

        private void CreateVariables(RunningDeployment context)
        {
            const string targetDiscoveryContext = @"{
    ""scope"": {
        ""spaceId"": ""Spaces-1"",
        ""environmentId"": ""Environments-1"",
        ""projectId"": ""Projects-1"",
        ""tenantId"": null,
        ""roles"": [""MyAzureAppRole""]
    },
    ""account"": {
        ""subscriptionNumber"": ""dad814cf-1c1e-4953-b950-c373a821c34f"",
        ""clientId"": ""a9ec7d56-5ed5-42d0-b5cf-bfdd33afa46e"",
        ""tenantId"": ""3d13e379-e666-469e-ac38-ec6fd61c1166"",
        ""password"": ""QGF7Q~SzsUG2sE~Hq3ewQ3d6LErD4TiuQUFfr"",
        ""azureEnvironment"": """",
        ""resourceManagementEndpointBaseUri"": """",
        ""activeDirectoryEndpointBaseUri"": """"
    }
}
";

            context.Variables.Add("Octopus.TargetDiscovery.Context", targetDiscoveryContext);
        }
    }
}