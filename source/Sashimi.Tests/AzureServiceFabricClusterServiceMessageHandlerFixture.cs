using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Data.Model;
using Octostache;
using Sashimi.AzureServiceFabric.Endpoints;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.Endpoints;
using Sashimi.Server.Contracts.ServiceMessages;
using AzureServiceFabricServiceMessageNames = Sashimi.AzureServiceFabric.AzureServiceFabricClusterServiceMessageHandler.AzureServiceFabricServiceMessageNames;

namespace Sashimi.AzureServiceFabric.Tests
{
    public class AzureServiceFabricClusterServiceMessageHandlerFixture
    {
        ICreateTargetServiceMessageHandler serviceMessageHandler = null!;

        [SetUp]
        public void SetUp()
        {
            serviceMessageHandler = new AzureServiceFabricClusterServiceMessageHandler();
        }

        [Test]
        public void Ctor_Properties_ShouldBeInitializedProperly()
        {
            serviceMessageHandler.AuditEntryDescription.Should().Be("Azure Service Fabric Target");
            serviceMessageHandler.ServiceMessageName.Should().Be(AzureServiceFabricServiceMessageNames.CreateTargetName);
        }

        [Test]
        [TestCase("")]
        [TestCase(null)]
        public void BuildEndpoint_WhenSecureModeIsSecureClientCertificateButUnableToResolveCertificateId_ShouldThrowException(
            string invalidCertificateId)
        {
            var messageProperties = GetMessagePropertiesBySecurityMode(AllAliasesForSecureClientCertificate().First());

            Action action = () => serviceMessageHandler.BuildEndpoint(messageProperties, new VariableDictionary(), null!,
                _ => invalidCertificateId, null!, null!);

            action.Should().Throw<Exception>().Which.Message.Should().Be(
                $"Certificate with Id / Name {messageProperties[AzureServiceFabricServiceMessageNames.CertificateIdOrNameAttribute]} not found.");
        }

        [Test]
        public void BuildEndpoint_WhenSecureModeIsSecureClientCertificateAndCertificateStoreNameIsMissing_ShouldReturnEndpointWithCorrectProperties()
        {
            var messageProperties = GetMessagePropertiesBySecurityMode(AllAliasesForSecureClientCertificate().First());
            messageProperties[AzureServiceFabricServiceMessageNames.CertificateStoreNameAttribute] = null!;

            const string certificateId = "Certificates-1";

            var endpoint = serviceMessageHandler.BuildEndpoint(messageProperties, new VariableDictionary(), null!, _ => certificateId, null!, null!);

            AssertEndpoint(endpoint, new ExpectedEndpointValues
            {
                SecurityMode = AzureServiceFabricSecurityMode.SecureClientCertificate,
                ConnectionEndpoint = messageProperties[AzureServiceFabricServiceMessageNames.ConnectionEndpointAttribute],
                ClientCertVariable = certificateId,
                ServerCertThumbprint = messageProperties[AzureServiceFabricServiceMessageNames.CertificateThumbprintAttribute],
                CertificateStoreLocation = messageProperties[AzureServiceFabricServiceMessageNames.CertificateStoreLocationAttribute],
                CertificateStoreName = "My"
            });
        }

        [Test]
        [TestCaseSource(nameof(AllAliasesForSecureClientCertificate))]
        public void BuildEndpoint_WhenSecureModeIsSecureClientCertificateAndCertificateStoreNameIsNotMissing_ShouldReturnEndpointWithCorrectProperties(
            string securityModeValue)
        {
            var messageProperties = GetMessagePropertiesBySecurityMode(securityModeValue);
            const string certificateId = "Certificates-1";

            var endpoint = serviceMessageHandler.BuildEndpoint(messageProperties, new VariableDictionary(), null!, _ => certificateId, null!, null!);

            AssertEndpoint(endpoint, new ExpectedEndpointValues
            {
                SecurityMode = AzureServiceFabricSecurityMode.SecureClientCertificate,
                ConnectionEndpoint = messageProperties[AzureServiceFabricServiceMessageNames.ConnectionEndpointAttribute],
                ClientCertVariable = certificateId,
                ServerCertThumbprint = messageProperties[AzureServiceFabricServiceMessageNames.CertificateThumbprintAttribute],
                CertificateStoreLocation = messageProperties[AzureServiceFabricServiceMessageNames.CertificateStoreLocationAttribute],
                CertificateStoreName = messageProperties[AzureServiceFabricServiceMessageNames.CertificateStoreNameAttribute]
            });
        }

        [Test]
        [TestCaseSource(nameof(AllAliasesForAzureActiveDirectory))]
        public void BuildEndpoint_WhenSecureModeIsAzureActiveDirectory_ShouldReturnEndpointWithCorrectProperties(
            string securityModeValue)
        {
            var messageProperties = GetMessagePropertiesBySecurityMode(securityModeValue);
            const string certificateId = "Certificates-3";

            var endpoint = serviceMessageHandler.BuildEndpoint(messageProperties, new VariableDictionary(), null!, _ => certificateId, null!, null!);

            AssertEndpoint(endpoint, new ExpectedEndpointValues
            {
                SecurityMode = AzureServiceFabricSecurityMode.SecureAzureAD,
                ConnectionEndpoint = messageProperties[AzureServiceFabricServiceMessageNames.ConnectionEndpointAttribute],
                ServerCertThumbprint = messageProperties[AzureServiceFabricServiceMessageNames.CertificateThumbprintAttribute],
                AadCredentialType = AzureServiceFabricCredentialType.UserCredential,
                AadUserCredentialUsername = messageProperties[AzureServiceFabricServiceMessageNames.ActiveDirectoryUsernameAttribute],
                AadUserCredentialPassword = messageProperties[AzureServiceFabricServiceMessageNames.ActiveDirectoryPasswordAttribute].ToSensitiveString()
            });
        }

        [Test]
        public void BuildEndpoint_WhenSecureModeIsUnSecure_ShouldReturnEndpointWithCorrectProperties()
        {
            var messageProperties = GetMessagePropertiesBySecurityMode("NotSecuredAtAll");

            const string certificateId = "Certificates-5";

            var endpoint = serviceMessageHandler.BuildEndpoint(messageProperties, new VariableDictionary(), null!, _ => certificateId, null!, null!);

            AssertEndpoint(endpoint, new ExpectedEndpointValues
            {
                SecurityMode = AzureServiceFabricSecurityMode.Unsecure,
                ConnectionEndpoint = messageProperties[AzureServiceFabricServiceMessageNames.ConnectionEndpointAttribute],
            });
        }

        [Test]
        public void BuildEndpoint_WhenWorkerPoolIsProvided_ShouldSetWorkerPoolId()
        {
            var messageProperties = GetMessagePropertiesBySecurityMode("NotSecuredAtAll");
            messageProperties.Add(AzureServiceFabricServiceMessageNames.WorkerPoolIdOrNameAttribute, "Worker Pool 1");

            const string certificateId = "Certificates-5";
            const string workerPoolId = "WorkerPools-5";

            var endpoint = serviceMessageHandler.BuildEndpoint(messageProperties, new VariableDictionary(), null!, _ => certificateId, _ => workerPoolId, null!);

            AssertEndpoint(endpoint, new ExpectedEndpointValues
            {
                SecurityMode = AzureServiceFabricSecurityMode.Unsecure,
                ConnectionEndpoint = messageProperties[AzureServiceFabricServiceMessageNames.ConnectionEndpointAttribute],
                WorkerPoolId = workerPoolId
            });
        }

        [Test]
        public void BuildEndpoint_WhenNoWorkerPoolIsProvided_ShouldUseStepWorkerPoolId()
        {
            var messageProperties = GetMessagePropertiesBySecurityMode("NotSecuredAtAll");
            messageProperties.Remove(AzureServiceFabricServiceMessageNames.WorkerPoolIdOrNameAttribute);

            const string certificateId = "Certificates-5";
            const string workerPoolId = "WorkerPools-6";

            var variableDictionary = new VariableDictionary();
            variableDictionary.Add(KnownVariables.WorkerPool.Id, workerPoolId);
            var endpoint = serviceMessageHandler.BuildEndpoint(messageProperties, variableDictionary, null!, _ => certificateId, _ => workerPoolId, null!);

            AssertEndpoint(endpoint, new ExpectedEndpointValues
            {
                SecurityMode = AzureServiceFabricSecurityMode.Unsecure,
                ConnectionEndpoint = messageProperties[AzureServiceFabricServiceMessageNames.ConnectionEndpointAttribute],
                WorkerPoolId = workerPoolId
            });
        }

        static void AssertEndpoint(Endpoint actualEndpoint, ExpectedEndpointValues expectedEndpointValues)
        {
            actualEndpoint.Should().BeOfType<AzureServiceFabricClusterEndpoint>();
            var serviceFabricClusterEndpoint = (AzureServiceFabricClusterEndpoint) actualEndpoint;
            serviceFabricClusterEndpoint.SecurityMode.Should().Be(expectedEndpointValues.SecurityMode);
            serviceFabricClusterEndpoint.ConnectionEndpoint.Should().Be(expectedEndpointValues.ConnectionEndpoint);

            switch (serviceFabricClusterEndpoint.SecurityMode)
            {
                case AzureServiceFabricSecurityMode.SecureClientCertificate:
                    serviceFabricClusterEndpoint.ClientCertVariable.Should().Be(expectedEndpointValues.ClientCertVariable);
                    serviceFabricClusterEndpoint.ServerCertThumbprint.Should().Be(expectedEndpointValues.ServerCertThumbprint);
                    serviceFabricClusterEndpoint.CertificateStoreLocation.Should().Be(expectedEndpointValues.CertificateStoreLocation);
                    serviceFabricClusterEndpoint.CertificateStoreName.Should().Be(expectedEndpointValues.CertificateStoreName);
                    break;
                case AzureServiceFabricSecurityMode.SecureAzureAD:
                    serviceFabricClusterEndpoint.ServerCertThumbprint.Should().Be(expectedEndpointValues.ServerCertThumbprint);
                    serviceFabricClusterEndpoint.AadUserCredentialUsername.Should().Be(expectedEndpointValues.AadUserCredentialUsername);
                    serviceFabricClusterEndpoint.AadUserCredentialPassword.Should().Be(expectedEndpointValues.AadUserCredentialPassword);
                    serviceFabricClusterEndpoint.AadCredentialType.Should().Be(expectedEndpointValues.AadCredentialType);
                    break;
            }

            serviceFabricClusterEndpoint.DefaultWorkerPoolId.Should().Be(expectedEndpointValues.WorkerPoolId);
        }

        static IDictionary<string, string> GetMessagePropertiesBySecurityMode(string securityModeValue)
        {
            var messageProperties = new Dictionary<string, string>
            {
                [AzureServiceFabricServiceMessageNames.SecurityModeAttribute] = securityModeValue,
                [AzureServiceFabricServiceMessageNames.ConnectionEndpointAttribute] = "Connection"
            };

            if (AllAliasesForSecureClientCertificate()
                .Any(a => a.Equals(securityModeValue, StringComparison.OrdinalIgnoreCase)))
            {
                messageProperties[AzureServiceFabricServiceMessageNames.CertificateIdOrNameAttribute] = "Certificate";
                messageProperties[AzureServiceFabricServiceMessageNames.CertificateThumbprintAttribute] = "Thumbprint";
                messageProperties[AzureServiceFabricServiceMessageNames.CertificateStoreLocationAttribute] = "Location";
                messageProperties[AzureServiceFabricServiceMessageNames.CertificateStoreNameAttribute] = "StoreName";

                return messageProperties;
            }

            if (AllAliasesForAzureActiveDirectory()
                .Any(a => a.Equals(securityModeValue, StringComparison.OrdinalIgnoreCase)))
            {
                messageProperties[AzureServiceFabricServiceMessageNames.CertificateThumbprintAttribute] = "Thumbprint";
                messageProperties[AzureServiceFabricServiceMessageNames.ActiveDirectoryUsernameAttribute] = "Username";
                messageProperties[AzureServiceFabricServiceMessageNames.ActiveDirectoryPasswordAttribute] = "Password";

                return messageProperties;
            }

            return messageProperties;
        }

        static IEnumerable<string> AllAliasesForSecureClientCertificate()
        {
            return new[] {"secureclientcertificate", "clientcertificate", "clientCertificate", "certificate", "certiFicate" };
        }

        static IEnumerable<string> AllAliasesForAzureActiveDirectory()
        {
            return new[] { "aad", "aAd", "azureactivedirectory", "azureActiveDirectory" };
        }

        class ExpectedEndpointValues
        {
            public AzureServiceFabricSecurityMode SecurityMode { get; set; }
            public string? ConnectionEndpoint { get; set; }
            public string? ClientCertVariable { get; set; }
            public string? ServerCertThumbprint { get; set; }
            public string? CertificateStoreLocation { get; set; }
            public string? CertificateStoreName { get; set; }
            public string? AadUserCredentialUsername { get; set; }
            public SensitiveString? AadUserCredentialPassword { get; set; }
            public AzureServiceFabricCredentialType AadCredentialType { get; set; }
            public string? WorkerPoolId { get; set; }
        }
    }
}