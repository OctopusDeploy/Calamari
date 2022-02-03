////////using Calamari.AzureAppService.Azure;
////using FluentAssertions;
////using FluentAssertions.Execution;
////using NUnit.Framework;
////using System;
////using System.Collections.Generic;
////using System.Linq;
////using System.Text;
////using System.Text.Json;
////using System.Threading.Tasks;

////namespace Calamari.AzureAppService.Tests.Azure
////{
////    [TestFixture]
////    public class TargetDiscoveryContextFixture
////    {
////        [Test]
////        public void JsonSerialization_RoundTripsCorrectly()
////        {
////            // Arrange
////            var json = @"{
////    ""scope"": {
////        ""spaceId"": ""Spaces-1"",
////        ""environmentId"": ""Environments-1"",
////        ""projectId"": ""Projects-1"",
////        ""tenantId"": null,
////        ""roles"": [""MyAzureAppRole""],
////        ""workerPoolId"": ""WorkerPools-1""
////    },
////    ""authentication"": {
////        ""accountId"": ""Accounts-1"",
////        ""account"": {
////            ""subscriptionNumber"": ""dad814cf-1c1e-4953-b950-c373a821c34f"",
////            ""clientId"": ""a9ec7d56-5ed5-42d0-b5cf-bfdd33afa46e"",
////            ""tenantId"": ""3d13e379-e666-469e-ac38-ec6fd61c1166"",
////            ""password"": ""QGF7Q~SzsUG2sE~Hq3ewQ3d6LErD4TiuQUFfr"",
////            ""azureEnvironment"": """",
////            ""resourceManagementEndpointBaseUri"": """",
////            ""activeDirectoryEndpointBaseUri"": """"
////        }
////    }
////}";
////            var options = new JsonSerializerOptions
////            {
////                PropertyNameCaseInsensitive = true
////            };

////            // Act
////            var sut = JsonSerializer.Deserialize<TargetDiscoveryContext>(json, options);

////            // Assert
////            using (new AssertionScope())
////            {
////                sut.Scope.SpaceId.Should().Be("Spaces-1");
////                sut.Scope.EnvironmentId.Should().Be("Environments-1");
////                sut.Scope.ProjectId.Should().Be("Projects-1");
////                sut.Scope.TenantId.Should().BeNull();
////                sut.Scope.Roles.Should().ContainSingle("MyAzureAppRole");
////                sut.Scope.WorkerPoolId.Should().Be("WorkerPools-1");
////                sut.Authentication.AccountId.Should().Be("Accounts-1");
////                sut.Authentication.Account.Should().NotBeNull();
////                sut.Authentication.Account.SubscriptionNumber.Should().Be("dad814cf-1c1e-4953-b950-c373a821c34f");
////                sut.Authentication.Account.ClientId.Should().Be("a9ec7d56-5ed5-42d0-b5cf-bfdd33afa46e");
////                sut.Authentication.Account.TenantId.Should().Be("3d13e379-e666-469e-ac38-ec6fd61c1166");
////                sut.Authentication.Account.Password.Should().Be("QGF7Q~SzsUG2sE~Hq3ewQ3d6LErD4TiuQUFfr");
////                sut.Authentication.Account.AzureEnvironment.Should().Be("");
////                sut.Authentication.Account.ResourceManagementEndpointBaseUri.Should().Be("");
////                sut.Authentication.Account.ActiveDirectoryEndpointBaseUri.Should().Be("");
////            }
////        }
////    }
////}
