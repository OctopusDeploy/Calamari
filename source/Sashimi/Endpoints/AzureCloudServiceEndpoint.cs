using System.Collections.Generic;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.Endpoints;
using Sashimi.Server.Contracts.Variables;

namespace Sashimi.AzureCloudService.Endpoints
{
    public class AzureCloudServiceEndpoint : Endpoint, IEndpointWithAccount, IRunsOnAWorker
    {
        public static readonly DeploymentTargetType AzureCloudServiceDeploymentTargetType = new DeploymentTargetType("AzureCloudService", "Azure Cloud Service");

        public override DeploymentTargetType DeploymentTargetType { get; } = AzureCloudServiceDeploymentTargetType;
        public override string Description => CloudServiceName;

        public override bool ScriptConsoleSupported => true;

        public string AccountId { get; set; } = string.Empty;

        public string CloudServiceName { get; set; } = string.Empty;
        public string StorageAccountName { get; set; } = string.Empty;
        public string Slot { get; set; } = string.Empty;
        public bool SwapIfPossible { get; set; }
        public bool UseCurrentInstanceCount { get; set; }

        public override string ToString() => Description;

        public override IEnumerable<Variable> ContributeVariables()
        {
            yield return new Variable(SpecialVariables.Action.Azure.CloudServiceName, CloudServiceName);
            yield return new Variable(SpecialVariables.Action.Azure.StorageAccountName, StorageAccountName);
            yield return new Variable(SpecialVariables.Action.Azure.Slot, Slot);
            yield return new Variable(SpecialVariables.Action.Azure.SwapIfPossible, SwapIfPossible.ToString());
            yield return new Variable(SpecialVariables.Action.Azure.UseCurrentInstanceCount, UseCurrentInstanceCount.ToString());
        }

        public override IEnumerable<(string id, DocumentType documentType)> GetRelatedDocuments()
        {
            if (!string.IsNullOrEmpty(AccountId))
                yield return (AccountId, DocumentType.Account);

            if (!string.IsNullOrEmpty(DefaultWorkerPoolId))
                yield return (DefaultWorkerPoolId, DocumentType.WorkerPool);
        }

        public string? DefaultWorkerPoolId { get; set; }
    }
}