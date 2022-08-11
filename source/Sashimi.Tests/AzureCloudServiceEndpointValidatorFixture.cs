using System;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Diagnostics;
using Octopus.Server.MessageContracts;
using Sashimi.AzureCloudService.Endpoints;
using Sashimi.Server.Contracts.Accounts;
using Sashimi.Tests.Shared.Extensions;

namespace Sashimi.AzureCloudService.Tests
{
    public class AzureCloudServiceEndpointValidatorFixture
    {
        [Test]
        public void Validate_FieldsNotPopulated_Error()
        {
            var errors = new AzureCloudServiceEndpointValidator().ValidateAndGetErrors(new AzureCloudServiceEndpoint());

            errors.Should().Contain("'Account' must not be empty.");
            errors.Should().Contain("'Cloud Service Name' must not be empty.");
            errors.Should().Contain("'Storage Account Name' must not be empty.");
        }

        [Test]
        public void Validate_FieldsPopulated_NoError()
        {
            var endpoint = new AzureCloudServiceEndpoint
            {
                AccountId = "blah",
                CloudServiceName = "the CloudService",
                StorageAccountName = "the storage"
            };

            var errors = new AzureCloudServiceEndpointValidator().ValidateAndGetErrors(endpoint);

            errors.Should().BeEmpty();
        }
    }

    public class AzureCertificateRequiresPrivateKeyFixture
    {
        [Test]
        public void CanContribute_AzureSubscriptionAccountResource_True()
        {
            var certificateEncoder = new CertificateEncoder(Substitute.For<ISystemLog>());
            var sut = new AzureCertificateRequiresPrivateKey(certificateEncoder);
            sut.CanContribute(new AzureSubscriptionAccountResource()).Should().BeTrue();
        }

        [Test]
        public void CanContribute_NotAzureSubscriptionAccountResource_False()
        {
            var certificateEncoder = new CertificateEncoder(Substitute.For<ISystemLog>());
            var sut = new AzureCertificateRequiresPrivateKey(certificateEncoder);
            sut.CanContribute(new TestAccountDetailsResource()).Should().BeFalse();
        }

        [Test]
        public void ValidateResource_NoCertificate_Success()
        {
            var certificateEncoder = new CertificateEncoder(Substitute.For<ISystemLog>());
            var sut = new AzureCertificateRequiresPrivateKey(certificateEncoder);
            sut.ValidateResource(new AzureSubscriptionAccountResource()).IsValid.Should().BeTrue();
        }

        [Test]
        public void ValidateResource_CertificateWithPrivateKey_Success()
        {
            var certificateEncoder = new CertificateEncoder(Substitute.For<ISystemLog>());
            var sut = new AzureCertificateRequiresPrivateKey(certificateEncoder);
            var accountResource = new AzureSubscriptionAccountResource();
            accountResource.CertificateBytes = certificateEncoder.ToBase64String(GenerateCertificate());
            sut.ValidateResource(accountResource).IsValid.Should().BeTrue();
        }

        [Test]
        public void ValidateResource_CertificateWithoutPrivateKey_Error()
        {
            var accountResource = new AzureSubscriptionAccountResource();

            var cert = GenerateCertificate();

            accountResource.CertificateBytes = new SensitiveValue
            {
                HasValue = true,
                NewValue = Convert.ToBase64String(cert.Export(X509ContentType.Cert))
            };

            var certificateEncoder = new CertificateEncoder(Substitute.For<ISystemLog>());
            var sut = new AzureCertificateRequiresPrivateKey(certificateEncoder);

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