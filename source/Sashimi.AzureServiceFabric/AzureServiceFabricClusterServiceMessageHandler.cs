using System;
using System.Collections.Generic;
using System.Linq;
using Octopus.Data.Model;
using Octostache;
using Sashimi.AzureServiceFabric.Endpoints;
using Sashimi.Server.Contracts.Accounts;
using Sashimi.Server.Contracts.Endpoints;
using Sashimi.Server.Contracts.ServiceMessages;

namespace Sashimi.AzureServiceFabric
{
    class AzureServiceFabricClusterServiceMessageHandler : ICreateTargetServiceMessageHandler
    {
        static readonly string[] SecurityModeSecureClientCertificateAliases =
            {"secureclientcertificate", "clientcertificate", "certificate"};

        static readonly string[] SecurityModeAzureActiveDirectoryAliases = {"aad", "azureactivedirectory"};

        public string AuditEntryDescription => "Azure Service Fabric Target";
        public string ServiceMessageName => AzureServiceFabricServiceMessageNames.CreateTargetName;

        public Endpoint BuildEndpoint(IDictionary<string, string> messageProperties,
                                      VariableDictionary variables,
                                      Func<string, string> accountIdResolver,
                                      Func<string, string> certificateIdResolver,
                                      Func<string, string> workerPoolIdResolver,
                                      Func<string, AccountType> accountTypeResolver)
        {
            messageProperties.TryGetValue(AzureServiceFabricServiceMessageNames.ConnectionEndpointAttribute, out var connectionEndpoint);
            var azureServiceFabricClusterEndpoint = new AzureServiceFabricClusterEndpoint
            {
                SecurityMode = GetSecurityMode(messageProperties),
                ConnectionEndpoint = connectionEndpoint
            };

            if (azureServiceFabricClusterEndpoint.SecurityMode == AzureServiceFabricSecurityMode.SecureClientCertificate)
            {
                messageProperties.TryGetValue(AzureServiceFabricServiceMessageNames.CertificateIdOrNameAttribute,
                    out var certificateIdOrName);
                if (!string.IsNullOrWhiteSpace(certificateIdOrName))
                {
                    var resolvedCertificateId = certificateIdResolver(certificateIdOrName);
                    if (string.IsNullOrWhiteSpace(resolvedCertificateId))
                    {
                        var message =
                            $"Certificate with Id / Name {certificateIdOrName} not found.";
                        throw new Exception(message);
                    }

                    azureServiceFabricClusterEndpoint.ClientCertVariable = resolvedCertificateId;
                }

                messageProperties.TryGetValue(AzureServiceFabricServiceMessageNames.CertificateThumbprintAttribute,
                    out var certificateThumbprint);
                messageProperties.TryGetValue(AzureServiceFabricServiceMessageNames.CertificateStoreLocationAttribute,
                    out var certificateStoreLocation);

                azureServiceFabricClusterEndpoint.ServerCertThumbprint = certificateThumbprint;
                azureServiceFabricClusterEndpoint.CertificateStoreLocation = certificateStoreLocation;
                azureServiceFabricClusterEndpoint.CertificateStoreName = GetCertificateStoreName(messageProperties);
            }
            
            if (azureServiceFabricClusterEndpoint.SecurityMode == AzureServiceFabricSecurityMode.SecureAzureAD)
            {
                messageProperties.TryGetValue(AzureServiceFabricServiceMessageNames.CertificateThumbprintAttribute,
                    out var certificateThumbprint);
                messageProperties.TryGetValue(AzureServiceFabricServiceMessageNames.ActiveDirectoryUsernameAttribute,
                    out var activeDirectoryUsername);
                messageProperties.TryGetValue(AzureServiceFabricServiceMessageNames.ActiveDirectoryPasswordAttribute,
                    out var activeDirectoryPassword);

                azureServiceFabricClusterEndpoint.ServerCertThumbprint = certificateThumbprint;
                azureServiceFabricClusterEndpoint.AadUserCredentialUsername = activeDirectoryUsername;
                azureServiceFabricClusterEndpoint.AadUserCredentialPassword = activeDirectoryPassword?.ToSensitiveString();
                azureServiceFabricClusterEndpoint.AadCredentialType = AzureServiceFabricCredentialType.UserCredential;
            }

            return azureServiceFabricClusterEndpoint;
        }

        static string GetCertificateStoreName(IDictionary<string, string> messageProperties)
        {
            messageProperties.TryGetValue(AzureServiceFabricServiceMessageNames.CertificateStoreNameAttribute, out var certificateStoreName);

            return string.IsNullOrWhiteSpace(certificateStoreName) ? "My" : certificateStoreName;
        }

        static AzureServiceFabricSecurityMode GetSecurityMode(IDictionary<string, string> messageProperties)
        {
            messageProperties.TryGetValue(AzureServiceFabricServiceMessageNames.SecurityModeAttribute, out var securityModeValue);
            
            if (SecurityModeSecureClientCertificateAliases.Any(x =>
                x.Equals(securityModeValue, StringComparison.OrdinalIgnoreCase)))
            {
                return AzureServiceFabricSecurityMode.SecureClientCertificate;
            }

            if (SecurityModeAzureActiveDirectoryAliases.Any(x =>
                x.Equals(securityModeValue, StringComparison.OrdinalIgnoreCase)))
            {
                return AzureServiceFabricSecurityMode.SecureAzureAD;
            }

            return AzureServiceFabricSecurityMode.Unsecure;
        }

        internal static class AzureServiceFabricServiceMessageNames
        {
            public const string CreateTargetName = "create-azureservicefabrictarget";
            public const string ConnectionEndpointAttribute = "connectionEndpoint";
            public const string SecurityModeAttribute = "securityMode";
            public const string CertificateThumbprintAttribute = "certificateThumbprint";
            public const string CertificateIdOrNameAttribute = "certificate";
            public const string ActiveDirectoryUsernameAttribute = "activeDirectoryUsername";
            public const string ActiveDirectoryPasswordAttribute = "activeDirectoryPassword";
            public const string CertificateStoreLocationAttribute = "certificateStoreLocation";
            public const string CertificateStoreNameAttribute = "certificateStoreName";
        }
    }
}