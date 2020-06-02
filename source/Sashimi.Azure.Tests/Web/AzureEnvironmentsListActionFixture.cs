using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Sashimi.Azure.Web;

namespace Sashimi.Azure.Tests.Web
{
    [TestFixture]
    public class AzureEnvironmentsListActionFixture
    {
        [Test]
        public async Task AllEnvironmentsHaveDisplayName()
        {
            var action = new AzureEnvironmentsListAction();

            var context = new TestOctoContext();
            await action.ExecuteAsync(context);

            var environments = context.TestResponse.GetResponse<IEnumerable<AzureEnvironmentResource>>();

            foreach (var azureEnvironmentResource in environments)
            {
                var errorMessage = $"Azure environment [{azureEnvironmentResource.Name}] Name and Display name are identical. This most likely means that the Azure SDK was updated, and with it new environments were added. Make sure to add a custom DisplayName for the new environment in {nameof(AzureEnvironmentsListAction)}.GetKnownEnvironmentDisplayName";

                azureEnvironmentResource.DisplayName.Should().NotBe(azureEnvironmentResource.Name, errorMessage);
            }
        }
    }
}