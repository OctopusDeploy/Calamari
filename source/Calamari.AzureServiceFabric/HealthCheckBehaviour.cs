using System;
using System.Fabric;
using System.Fabric.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.Variables;
using Microsoft.Identity.Client;

namespace Calamari.AzureServiceFabric
{
    class HealthCheckBehaviour: IDeployBehaviour
    {
        readonly IVariables variables;
        readonly ILog log;

        public HealthCheckBehaviour(IVariables variables, ILog log)
        {
            this.variables = variables;
            this.log = log;
        }

        public bool IsEnabled(RunningDeployment context)
        {
            return true;
        }

        public async Task Execute(RunningDeployment context)
        {
            if (!ServiceFabricHelper.IsServiceFabricSdkKeyInRegistry())
            {
                throw new Exception("Could not find the Azure Service Fabric SDK on this server. This SDK is required before running health checks on Service Fabric targets.");
            }

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
            FabricClient fabricClient;

            switch (securityMode)
            {
                case AzureServiceFabricSecurityMode.SecureClientCertificate:
                {
                    log.Info("Connecting with Secure Client Certificate");

                    var clientCertThumbprint = variables.Get(clientCertVariable + ".Thumbprint");
                    var commonName = variables.Get(clientCertVariable + ".SubjectCommonName");

                    CalamariCertificateStore.EnsureCertificateIsInstalled(variables, clientCertVariable, certificateStoreName, certificateStoreLocation);

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

                case AzureServiceFabricSecurityMode.SecureAD:
                {
                    log.Info("Connecting with Secure Azure Active Directory");
                    log.Verbose("Using the service account of the octopus service as windows credentials");
                    var windowsCredentials = new WindowsCredentials();
                    fabricClient = new FabricClient(windowsCredentials, connectionEndpoint);
                    break;
                }

                default:
                {
                    log.Info("Connecting insecurely");
                    fabricClient = new FabricClient(connectionEndpoint);
                    break;
                }
            }

            if (fabricClient == null)
            {
                throw new Exception("Unable to create Service Fabric client.");
            }

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

        #region Auth helpers
        static string GetAccessToken(AzureActiveDirectoryMetadata aad, string aadUsername, string aadPassword)
        {
            var app = PublicClientApplicationBuilder
                      .Create(aad.ClientApplication)
                      .WithAuthority(aad.Authority)
                      .Build();

            var authResult = app.AcquireTokenByUsernamePassword(new[] { $"{aad.ClientApplication}/.default" }, aadUsername, aadPassword)
                                .ExecuteAsync()
                                .GetAwaiter()
                                .GetResult();

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