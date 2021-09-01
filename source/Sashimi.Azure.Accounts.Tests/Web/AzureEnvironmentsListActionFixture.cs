using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Server.Extensibility.Extensions.Infrastructure.Web.Api;
using Sashimi.Azure.Accounts.Web;

namespace Sashimi.Azure.Accounts.Tests.Web
{
    [TestFixture]
    public class AzureEnvironmentsListActionFixture
    {
        [Test]
        public async Task AllEnvironmentsHaveDisplayName()
        {
            var action = new AzureEnvironmentsListAction();

            var request = Substitute.For<IOctoRequest>();
            var responseProvider = await action.ExecuteAsync(request);

            var response = responseProvider.Response;
            response.Should().BeOfType<OctoDataResponse>();

            var environments = (IReadOnlyCollection<AzureEnvironmentResource>)((OctoDataResponse)response).Model;

            foreach (var azureEnvironmentResource in environments)
            {
                var errorMessage = $"Azure environment [{azureEnvironmentResource.Name}] Name and Display name are identical. This most likely means that the Azure SDK was updated, and with it new environments were added. Make sure to add a custom DisplayName for the new environment in {nameof(AzureEnvironmentsListAction)}.GetKnownEnvironmentDisplayName";

                azureEnvironmentResource.DisplayName.Should().NotBe(azureEnvironmentResource.Name, errorMessage);
            }
        }
    }
}