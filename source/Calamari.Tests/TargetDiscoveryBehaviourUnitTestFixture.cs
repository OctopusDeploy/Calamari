using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Variables;
using Calamari.Tests.Shared.Helpers;
using FluentAssertions;
using NUnit.Framework;
using System.Threading.Tasks;

namespace Calamari.AzureAppService.Tests
{
    [TestFixture]
    public class TargetDiscoveryBehaviourUnitTestFixture
    {
        [Test]
        public async Task Exectute_LogsError_WhenContextIsMissing()
        {
            // Arrange
            var variables = new CalamariVariables();
            var context = new RunningDeployment(variables);
            var log = new InMemoryLog();
            var sut = new TargetDiscoveryBehaviour(log);

            // Act
            await sut.Execute(context);

            // Assert
            log.StandardOut.Should().Contain("Error: target discovery scope missing.");
        }

//////        private void CreateVariables(RunningDeployment context)
//////        {
//////            string targetDiscoveryContext = $@"{{
//////    ""scope"": {{
//////        ""spaceName"": ""default"",
//////        ""environmentName"": ""dev"",
//////        ""projectName"": ""my-test-project"",
//////        ""tenantName"": null,
//////        ""roles"": [""my-azure-app-role""]
//////    }},
//////    ""authentication"": {{
//////        ""accountId"": ""Accounts-1"",
//////        ""accountDetails"": {{
//////            ""subscriptionNumber"": ""{subscriptionId}"",
//////            ""clientId"": ""{clientId}"",
//////            ""tenantId"": ""{tenantId}"",
//////            ""password"": ""{clientSecret}"",
//////            ""azureEnvironment"": """",
//////            ""resourceManagementEndpointBaseUri"": """",
//////            ""activeDirectoryEndpointBaseUri"": """"
//////        }}
//////    }}
//////}}
//////";

////            context.Variables.Add("Octopus.TargetDiscovery.Context", targetDiscoveryContext);
////            //////context.Variables.Add("Octopus.Account.Id", "Account-1");
////            ////context.Variables.Add("Octopus.WorkerPool.Id", "WorkerPools-1");
////        }
    }
}