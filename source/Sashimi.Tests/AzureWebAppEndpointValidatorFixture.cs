#nullable disable
using FluentAssertions;
using FluentValidation;
using NUnit.Framework;
using Sashimi.AzureAppService.Endpoints;
using Sashimi.Tests.Shared.Extensions;

namespace Sashimi.AzureAppService.Tests
{
    public class AzureWebAppEndpointValidatorFixture
    {
        IValidator sut;
        AzureWebAppEndpoint endpoint;

        [SetUp]
        public void Setup()
        {
            sut = new AzureWebAppEndpointValidator();
            endpoint = new AzureWebAppEndpoint();
        }

        [Test]
        public void Validate_FieldsNotPopulated_Error()
        {
            var errors = sut.ValidateAndGetErrors(endpoint);

            errors.Should().Contain("'Account' must not be empty.");
            errors.Should().Contain("'Web App' must not be empty.");
            errors.Should().Contain("'Resource Group' must not be empty.");
        }

        [Test]
        public void Validate_FieldsPopulated_NoError()
        {
            endpoint.AccountId = "blah";
            endpoint.WebAppName = "the webapp";
            endpoint.ResourceGroupName = "the group";

            var errors = sut.ValidateAndGetErrors(endpoint);

            errors.Should().BeEmpty();
        }
    }
}