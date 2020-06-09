using FluentAssertions;
using FluentValidation;
using NUnit.Framework;
using Sashimi.AzureCloudService.Endpoints;
using Sashimi.Tests.Shared.Extensions;

namespace Sashimi.AzureCloudService.Tests
{
    public class AzureCloudServiceEndpointValidatorFixture
    {
        IValidator sut;
        AzureCloudServiceEndpoint endpoint;

        [SetUp]
        public void Setup()
        {
            sut = new AzureCloudServiceEndpointValidator();
            endpoint = new AzureCloudServiceEndpoint();
        }

        [Test]
        public void Validate_FieldsNotPopulated_Error()
        {
            var errors = sut.ValidateAndGetErrors(endpoint);

            errors.Should().Contain("'Account' must not be empty.");
            errors.Should().Contain("'Cloud Service Name' must not be empty.");
            errors.Should().Contain("'Storage Account Name' must not be empty.");
        }

        [Test]
        public void Validate_FieldsPopulated_NoError()
        {
            endpoint.AccountId = "blah";
            endpoint.CloudServiceName = "the CloudService";
            endpoint.StorageAccountName = "the storage";

            var errors = sut.ValidateAndGetErrors(endpoint);

            errors.Should().BeEmpty();
        }
    }
}