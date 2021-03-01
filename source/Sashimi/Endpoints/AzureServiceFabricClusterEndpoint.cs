using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Octopus.Data.Model;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.Endpoints;
using Sashimi.Server.Contracts.Variables;

namespace Sashimi.AzureServiceFabric.Endpoints
{
    public class AzureServiceFabricClusterEndpoint : Endpoint, IEndpointWithExpandableCertificate, IRunsOnAWorker, IEndpointWithClientCertificates
    {
        public static readonly DeploymentTargetType AzureServiceFabricClusterDeploymentTargetType = new DeploymentTargetType("AzureServiceFabricCluster", "Azure Service Fabric Cluster");

        public override DeploymentTargetType DeploymentTargetType { get; } = AzureServiceFabricClusterDeploymentTargetType;
        public override string Description => ConnectionEndpoint ?? string.Empty;

        public string? ConnectionEndpoint { get; set; }
        public AzureServiceFabricSecurityMode SecurityMode { get; set; }
        public string? ServerCertThumbprint { get; set; }
        public string? ClientCertVariable { get; set; }
        public string? CertificateStoreLocation { get; set; }
        public string? CertificateStoreName { get; set; }
        public AzureServiceFabricCredentialType? AadCredentialType { get; set; }
        public string? AadClientCredentialSecret { get; set; }
        public string? AadUserCredentialUsername { get; set; }

        public SensitiveString? AadUserCredentialPassword { get; set; }

        public override IEnumerable<Variable> ContributeVariables()
        {
            yield return new Variable(SpecialVariables.Action.ServiceFabric.ConnectionEndpoint, ConnectionEndpoint);
            yield return new Variable(SpecialVariables.Action.ServiceFabric.SecurityMode, SecurityMode.ToString());
            yield return new Variable(SpecialVariables.Action.ServiceFabric.ServerCertThumbprint, ServerCertThumbprint);
            yield return new Variable(SpecialVariables.Action.ServiceFabric.ClientCertVariable, ClientCertVariable);
            yield return new Variable(SpecialVariables.Action.ServiceFabric.CertificateStoreLocation, !string.IsNullOrWhiteSpace(CertificateStoreLocation) ? CertificateStoreLocation : StoreLocation.LocalMachine.ToString());
            yield return new Variable(SpecialVariables.Action.ServiceFabric.CertificateStoreName, !string.IsNullOrWhiteSpace(CertificateStoreName) ? CertificateStoreName : "My");
            yield return new Variable(SpecialVariables.Action.ServiceFabric.AadCredentialType, AadCredentialType.ToString());
            yield return new Variable(SpecialVariables.Action.ServiceFabric.AadClientCredentialSecret, AadClientCredentialSecret, VariableType.Sensitive);
            yield return new Variable(SpecialVariables.Action.ServiceFabric.AadUserCredentialUsername, AadUserCredentialUsername);
            yield return new Variable(SpecialVariables.Action.ServiceFabric.AadUserCredentialPassword, AadUserCredentialPassword);

            if (!string.IsNullOrEmpty(ClientCertVariable))
            {
                // Certificate variables need to be handled a little differently so they expand like project variables (I.e. By name).
                // This maintains backwards compatibility with certificates that used to be defined on the step.
                yield return new Variable(ClientCertVariable, ClientCertVariable, VariableType.Certificate);
            }
        }

        public string? DefaultWorkerPoolId { get; set; } = string.Empty;

        public override IEnumerable<(string id, DocumentType documentType)> GetRelatedDocuments()
        {
            if (!string.IsNullOrEmpty(DefaultWorkerPoolId))
                yield return (DefaultWorkerPoolId, DocumentType.WorkerPool);
        }

        public IEnumerable<string> ClientCertificateIds => new[] { ClientCertVariable }.Where(c => !string.IsNullOrEmpty(c)).Cast<string>();
    }
}
