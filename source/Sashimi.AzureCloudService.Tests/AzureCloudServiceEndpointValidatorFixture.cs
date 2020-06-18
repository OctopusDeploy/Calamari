using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Calamari.Tests.Shared;
using FluentAssertions;
using FluentValidation;
using NUnit.Framework;
using Octopus.Data.Resources;
using Sashimi.AzureCloudService.Endpoints;
using Sashimi.Server.Contracts.Accounts;
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

    public class AzureCertificateRequiresPrivateKeyFixture
    {
        AzureCertificateRequiresPrivateKey sut;
        CertificateEncoder certificateEncoder;

        [SetUp]
        public void SetUp()
        {
            certificateEncoder = new CertificateEncoder(new ServerInMemoryLog());
            sut = new AzureCertificateRequiresPrivateKey(certificateEncoder);
        }

        [Test]
        public void CanContribute_AzureSubscriptionAccountResource_True()
        {
            sut.CanContribute(new AzureSubscriptionAccountResource()).Should().BeTrue();
        }

        [Test]
        public void CanContribute_NotAzureSubscriptionAccountResource_False()
        {
            sut.CanContribute(new TestAccountDetailsResource()).Should().BeFalse();
        }

        [Test]
        public void ValidateResource_NoCertificate_Success()
        {
            sut.ValidateResource(new AzureSubscriptionAccountResource()).IsValid.Should().BeTrue();
        }

        [Test]
        public void ValidateResource_CertificateWithPrivateKey_Success()
        {
            var accountResource = new AzureSubscriptionAccountResource();
            accountResource.CertificateBytes = certificateEncoder.ToBase64String(GenerateCertificate());

            sut.ValidateResource(accountResource).IsValid.Should().BeTrue();
        }

        [Test]
        public void ValidateResource_CertificateWithoutPrivateKey_Error()
        {
            var accountResource = new AzureSubscriptionAccountResource();

            var cert = GenerateCertificate();

            accountResource.CertificateBytes = new SensitiveValue()
            {
                HasValue = true,
                NewValue = Convert.ToBase64String(cert.Export(X509ContentType.Cert))
            };

            var result = sut.ValidateResource(accountResource);
            result.IsValid.Should().BeFalse();
            result.ErrorMessage.Should().Be("The X509 Certificate file lacks the private key. Please provide a file that includes the private key.");
        }

        static X509Certificate2 GenerateCertificate()
        {
            return new CertificateGenerator().GenerateNew(CertificateExpectations.BuildOctopusAzureCertificateFullName("blah"));
        }

        class TestAccountDetailsResource : AccountDetailsResource
        {
            public override AccountType AccountType => new AccountType("blah");
        }
    }
}