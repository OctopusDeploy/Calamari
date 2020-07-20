using System;
using FluentAssertions;
using FluentValidation;
using NUnit.Framework;
using Octopus.Data.Model;
using Sashimi.AzureServiceFabric.Endpoints;
using Sashimi.Tests.Shared.Extensions;

namespace Sashimi.AzureServiceFabric.Tests
{
    public class AzureServiceFabricClusterEndpointValidatorFixture
    {
        IValidator sut;
        AzureServiceFabricClusterEndpoint endpoint;

        [SetUp]
        public void Setup()
        {
            sut = new AzureServiceFabricClusterEndpointValidator();
            endpoint = new AzureServiceFabricClusterEndpoint
            {
                ConnectionEndpoint = "conn endpoint"
            };
        }

        [Test]
        public void Validate_UnsecureFieldsNotPopulated_Error()
        {
            endpoint.ConnectionEndpoint = null;
            var errors = sut.ValidateAndGetErrors(endpoint);

            errors.Should().Contain("'Connection Endpoint' must not be empty.");
        }

        [Test]
        public void Validate_UnsecureFieldsPopulated_NoError()
        {
            var errors = sut.ValidateAndGetErrors(endpoint);

            errors.Should().BeEmpty();
        }

        [Test]
        public void Validate_SecureAzureADUsingClientCredential_Error()
        {
            endpoint.SecurityMode = AzureServiceFabricSecurityMode.SecureAzureAD;
            endpoint.AadCredentialType = AzureServiceFabricCredentialType.ClientCredential;

            var errors = sut.ValidateAndGetErrors(endpoint);

            errors.Should().Contain("'Azure AD Credential Type' must not be equal to 'ClientCredential'.");
        }

        [Test]
        public void Validate_SecureAzureADUsingUserCredentialFieldsNotPopulated_Error()
        {
            endpoint.SecurityMode = AzureServiceFabricSecurityMode.SecureAzureAD;
            endpoint.AadCredentialType = AzureServiceFabricCredentialType.UserCredential;

            var errors = sut.ValidateAndGetErrors(endpoint);

            errors.Should().Contain("'Azure AD User Credential Username' must not be empty.");
            errors.Should().Contain("'Azure AD User Credential Password' must not be empty.");
            errors.Should().Contain("'Server Cert Thumbprint' must not be empty.");
        }

        [Test]
        public void Validate_SecureAzureADUsingUserCredentialFieldsPopulated_NoError()
        {
            endpoint.SecurityMode = AzureServiceFabricSecurityMode.SecureAzureAD;
            endpoint.AadCredentialType = AzureServiceFabricCredentialType.UserCredential;
            endpoint.ClientCertVariable = "abc";
            endpoint.ServerCertThumbprint = "abc";
            endpoint.AadUserCredentialUsername = "user";
            endpoint.AadUserCredentialPassword = "pass".ToSensitiveString();
            var errors = sut.ValidateAndGetErrors(endpoint);

            errors.Should().BeEmpty();
        }

        [Test]
        public void Validate_SecureClientCertificateUsingClientCredentialFieldsNotPopulated_Error()
        {
            endpoint.SecurityMode = AzureServiceFabricSecurityMode.SecureClientCertificate;

            var errors = sut.ValidateAndGetErrors(endpoint);

            errors.Should().Contain("'Client Cert Variable' must not be empty.");
            errors.Should().Contain("'Server Cert Thumbprint' must not be empty.");
        }

        [Test]
        public void Validate_SecureClientCertificateUsingClientCredentialFieldsPopulated_NoError()
        {
            endpoint.SecurityMode = AzureServiceFabricSecurityMode.SecureClientCertificate;
            endpoint.ClientCertVariable = "abc";
            endpoint.ServerCertThumbprint = "123";
            var errors = sut.ValidateAndGetErrors(endpoint);

            errors.Should().BeEmpty();
        }
    }
}