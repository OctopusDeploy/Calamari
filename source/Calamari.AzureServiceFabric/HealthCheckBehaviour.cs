using System;
using System.Fabric;
using System.Threading.Tasks;
using Calamari.AzureServiceFabric.Behaviours;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.Variables;
using Calamari.Integration.Certificates;

namespace Calamari.AzureServiceFabric
{
    class HealthCheckBehaviour: IDeployBehaviour
    {
        readonly ICertificateStore certificateStore;
        readonly IVariables variables;
        readonly ILog log;

        public HealthCheckBehaviour(ICertificateStore certificateStore, IVariables variables, ILog log)
        {
            this.certificateStore = certificateStore;
            this.variables = variables;
            this.log = log;
        }

        public bool IsEnabled(RunningDeployment context)
        {
            return true;
        }

        public async Task Execute(RunningDeployment context)
        {
            if (!ServiceFabricHelper.IsServiceFabricSdkInstalled())
            {
                throw new Exception("Could not find the Azure Service Fabric SDK on this server. This SDK is required before running health checks on Service Fabric targets.");
            }

            var fabricClient = await GetFabricClient(context);
            try
            {
                await fabricClient.ClusterManager.GetClusterManifestAsync();
                log.Verbose("Successfully received a response from the Service Fabric client");
            }
            finally
            {
                fabricClient.Dispose();
            }
        }

        async Task<FabricClient> GetFabricClient(RunningDeployment context)
        {
            var connectionEndpoint = variables.Get(SpecialVariables.Action.ServiceFabric.ConnectionEndpoint);
            var securityMode = (AzureServiceFabricSecurityMode)Enum.Parse(typeof(AzureServiceFabricSecurityMode), variables.Get(SpecialVariables.Action.ServiceFabric.SecurityMode));
            
            log.Verbose($"Checking connectivity to Service Fabric cluster '{connectionEndpoint}' with security-mode '{securityMode}'");

            switch (securityMode)
            {
                case AzureServiceFabricSecurityMode.SecureClientCertificate:
                    return await GetSecureClientCertificateClient(context, connectionEndpoint);
                case AzureServiceFabricSecurityMode.SecureAzureAD:
                    return GetSecureAzureADClient(connectionEndpoint);
                case AzureServiceFabricSecurityMode.SecureAD:
                    return GetSecureADClient(connectionEndpoint);
                case AzureServiceFabricSecurityMode.Unsecure:
                default:
                {
                    log.Info("Connecting insecurely");
                     return new FabricClient(connectionEndpoint);
                }
            }
        }

        FabricClient GetSecureADClient(string connectionEndpoint)
        {
            log.Info("Connecting with Secure Azure Active Directory");
            log.Verbose("Using the service account of the octopus service as windows credentials");
            var windowsCredentials = new WindowsCredentials();
            return new FabricClient(windowsCredentials, connectionEndpoint);
        }

        FabricClient GetSecureAzureADClient(string connectionEndpoint)
        {
            log.Info("Connecting with Secure Azure Active Directory");
            var aadUserCredentialUsername = variables.Get(SpecialVariables.Action.ServiceFabric.AadUserCredentialUsername);
            var aadUserCredentialPassword = variables.Get(SpecialVariables.Action.ServiceFabric.AadUserCredentialPassword);
            var serverCertThumbprint = variables.Get(SpecialVariables.Action.ServiceFabric.ServerCertThumbprint);
                    
            var claimsCredentials = new ClaimsCredentials();
            claimsCredentials.ServerThumbprints.Add(serverCertThumbprint);
            var fabricClient = new FabricClient(claimsCredentials, connectionEndpoint);
            fabricClient.ClaimsRetrieval += (o, e) => AzureADUsernamePasswordTokenRetriever.GetAccessToken(e.AzureActiveDirectoryMetadata, aadUserCredentialUsername, aadUserCredentialPassword, log);
            return fabricClient;
        }

        async Task<FabricClient> GetSecureClientCertificateClient(RunningDeployment context, string connectionEndpoint)
        {
            log.Info("Connecting with Secure Client Certificate");

            var clientCertVariable = variables.Get(SpecialVariables.Action.ServiceFabric.ClientCertVariable);
            var clientCertThumbprint = variables.Get(clientCertVariable + ".Thumbprint");
            var commonName = variables.Get(clientCertVariable + ".SubjectCommonName");
            var certStoreLocation = variables.GetServiceFabricCertificateStoreLocation();
            var certstoreName = variables.GetServiceFabricCertificateStoreName();
            var serverCertThumbprint = variables.Get(SpecialVariables.Action.ServiceFabric.ServerCertThumbprint);
                    
            await new EnsureCertificateInstalledInStoreBehaviour(certificateStore).Execute(context);

            var xc = ServiceFabricHelper.GetX509Credentials(clientCertThumbprint, certStoreLocation, certstoreName.ToString(), serverCertThumbprint, commonName);
            try
            {
                return new FabricClient(xc, connectionEndpoint);
            }
            catch (Exception ex)
            {
                // SF throw weird exception messages if you don't have the certificate installed.
                if (ex.InnerException != null && ex.InnerException.Message.Contains("0x80071C57"))
                    throw new Exception($"Service Fabric was unable to to find certificate with thumbprint '{clientCertThumbprint}' in Cert:\\{certStoreLocation}\\{certstoreName}. Please make sure you have installed the certificate on the Octopus Server before attempting to use/reference it in a Service Fabric Cluster target.");
                throw;
            }
        }
    }
    
}