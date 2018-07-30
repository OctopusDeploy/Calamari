using System;
using System.Fabric;
using System.Fabric.Security;
using System.Security.Cryptography.X509Certificates;
using Calamari.Azure.Util;
using Calamari.Deployment;
using Calamari.HealthChecks;
using Calamari.Integration.Certificates;
using Calamari.Integration.Processes;
using Calamari.Util;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace Calamari.Azure.HealthChecks
{
    public class ServiceFabricHealthChecker : IDoesDeploymentTargetTypeHealthChecks
    {
        private readonly ILog log;
        private readonly ICertificateStore certificateStore;

        public ServiceFabricHealthChecker(ILog log, ICertificateStore certificateStore)
        {
            this.log = log;
            this.certificateStore = certificateStore;
        }

        public bool HandlesDeploymentTargetTypeName(string deploymentTargetTypeName)
        {
            return deploymentTargetTypeName == "AzureServiceFabricCluster";
        }

        public int ExecuteHealthCheck(CalamariVariableDictionary variables)
        {
            var connectionEndpoint = variables.Get(SpecialVariables.Action.ServiceFabric.ConnectionEndpoint);
            var securityMode = (AzureServiceFabricSecurityMode)Enum.Parse(typeof(AzureServiceFabricSecurityMode), variables.Get(SpecialVariables.Action.ServiceFabric.SecurityMode));
            var serverCertThumbprint = variables.Get(SpecialVariables.Action.ServiceFabric.ServerCertThumbprint);

            var clientCertVariable = variables.Get(SpecialVariables.Action.ServiceFabric.ClientCertVariable);

            var certificateStoreLocation = variables.Get(SpecialVariables.Action.ServiceFabric.CertificateStoreLocation);
            if (string.IsNullOrWhiteSpace(certificateStoreLocation))
                certificateStoreLocation = StoreLocation.LocalMachine.ToString();

            var certificateStoreName = variables.Get(SpecialVariables.Action.ServiceFabric.CertificateStoreName);
            if (string.IsNullOrWhiteSpace(certificateStoreName))
                certificateStoreName = "My";

            var aadUserCredentialUsername = variables.Get(SpecialVariables.Action.ServiceFabric.AadUserCredentialUsername);
            var aadUserCredentialPassword = variables.Get(SpecialVariables.Action.ServiceFabric.AadUserCredentialPassword);

            log.Verbose($"Checking connectivity to Service Fabric cluster '{connectionEndpoint}' with security-mode '{securityMode}'");
            FabricClient fabricClient = null;
            // ReSharper disable once SwitchStatementMissingSomeCases
            switch (securityMode)
            {
                case AzureServiceFabricSecurityMode.SecureClientCertificate:
                    {
                        log.Info("Connecting with Secure Client Certificate");

                        var clientCertThumbprint = variables.Get(clientCertVariable + ".Thumbprint");
                        var certificateBytes = variables.Get(clientCertVariable + ".Certificate");
                        var password = variables.Get(clientCertVariable + ".Password");
                        var commonName = variables.Get(clientCertVariable + ".SubjectCommonName");

                        EnsureServiceFabricCertificateExistsInStore(clientCertThumbprint, certificateBytes, password, certificateStoreLocation, certificateStoreName, false);

                        var xc = GetCredentials(clientCertThumbprint, certificateStoreLocation, certificateStoreName, serverCertThumbprint, commonName);
                        try
                        {
                            fabricClient = new FabricClient(xc, connectionEndpoint);
                        }
                        catch (Exception ex)
                        {
                            // SF throw weird exception messages if you don't have the certificate installed.
                            if (ex.InnerException != null && ex.InnerException.Message.Contains("0x80071C57"))
                                throw new Exception($"Service Fabric was unable to to find certificate with thumbprint '{clientCertThumbprint}' in Cert:\\{certificateStoreLocation}\\{certificateStoreName}. Please make sure you have installed the certificate on the Octopus Server before attempting to use/reference it in a Service Fabric Cluster target.");
                            throw;
                        }
                        break;
                    }
                case AzureServiceFabricSecurityMode.SecureAzureAD:
                    {
                        log.Info("Connecting with Secure Azure Active Directory");
                        var claimsCredentials = new ClaimsCredentials();
                        claimsCredentials.ServerThumbprints.Add(serverCertThumbprint);
                        // ReSharper disable once UseObjectOrCollectionInitializer
                        fabricClient = new FabricClient(claimsCredentials, connectionEndpoint);
                        fabricClient.ClaimsRetrieval += (o, e) =>
                        {
                            try
                            {
                                return GetAccessToken(e.AzureActiveDirectoryMetadata, aadUserCredentialUsername, aadUserCredentialPassword);
                            }
                            catch (Exception ex)
                            {
                                log.Error($"Connect failed: {ex.PrettyPrint()}");
                                return "BAD_TOKEN"; //TODO: mark.siedle - You cannot return null or an empty value here or the Azure lib spazzes out trying to call a lib that doesn't exist "System.Fabric.AzureActiveDirectory.Client"  :(
                            }
                        };
                        break;
                    }
                default:
                    {
                        log.Info("Connecting unsecurely");
                        fabricClient = new FabricClient(connectionEndpoint);
                        break;
                    }
            }

            if (fabricClient == null)
                throw new Exception("Unable to create Service Fabric client.");

            try
            {
                fabricClient.ClusterManager.GetClusterManifestAsync().GetAwaiter().GetResult();
                log.Verbose("Successfully received a response from the Service Fabric client");
            }
            finally
            {
                fabricClient.Dispose();
            }

            return 0;
        }

        void EnsureServiceFabricCertificateExistsInStore(string thumbprint, string certificateBytes, string password, string certificateStoreLocation, string certificateStoreName, bool throwOnFail)
        {
            try
            {
                var storeName = (StoreName)Enum.Parse(typeof(StoreName), certificateStoreName);
                var storeLocation = (StoreLocation)Enum.Parse(typeof(StoreLocation), certificateStoreLocation);
                certificateStore.GetOrAdd(thumbprint, certificateBytes, storeName, storeLocation, password);
            }
            catch (Exception ex)
            {
                log.Warn($"Failed to add or confirm whether the Service Fabric certificate was available in the expected store (this warning can be ignored for self-signed certificates).\n{ex.PrettyPrint()}");
                if (throwOnFail)
                    throw;
            }
        }

        #region Auth helpers

        static string GetAccessToken(AzureActiveDirectoryMetadata aad, string aadUsername, string aadPassword)
        {
            var authContext = new AuthenticationContext(aad.Authority);
            var authResult = authContext.AcquireToken(
                aad.ClusterApplication,
                aad.ClientApplication,
                new UserCredential(aadUsername, aadPassword));
            return authResult.AccessToken;
        }

        static X509Credentials GetCredentials(string clientCertThumbprint, string clientCertStoreLocation, string clientCertStoreName, string serverCertThumb, string commonName)
        {
            var xc = new X509Credentials
            {
                StoreLocation = (StoreLocation)Enum.Parse(typeof(StoreLocation), clientCertStoreLocation),
                StoreName = clientCertStoreName,
                FindType = X509FindType.FindByThumbprint,
                FindValue = clientCertThumbprint
            };
            xc.RemoteCommonNames.Add(commonName);
            xc.RemoteCertThumbprints.Add(serverCertThumb);
            xc.ProtectionLevel = ProtectionLevel.EncryptAndSign;
            return xc;
        }

        #endregion
    }
}