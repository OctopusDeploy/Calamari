using System;
using System.Collections.Generic;
using System.Text;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.Endpoints;
using Sashimi.Server.Contracts.Variables;

namespace Sashimi.AzureWebAppZip.Endpoints
{
    public class AzureWebAppEndpoint : Endpoint, IRunsOnAWorker
    {
        public string WebAppName { get; set; } = string.Empty;

        public string ResourceGroupName { get; set; } = string.Empty;

        public string? WebAppSlotName { get; set; } = string.Empty;

        public static readonly DeploymentTargetType AzureWebAppDeploymentTargetType =
            new DeploymentTargetType("AzureWebApp", "Azure Web Application");

        public override DeploymentTargetType DeploymentTargetType { get; } = AzureWebAppDeploymentTargetType;

        public override string Description => WebAppName;

        public override bool ScriptConsoleSupported => true;

        public string DefaultWorkerPoolId { get; set; } = string.Empty;

        public override IEnumerable<(string id, DocumentType documentType)> GetRelatedDocuments()
        {
            if (!string.IsNullOrEmpty(DefaultWorkerPoolId))
                yield return (DefaultWorkerPoolId, DocumentType.WorkerPool);
        }

        public override IEnumerable<Variable> ContributeVariables()
        {
            yield return new Variable(SpecialVariables.Action.Azure.WebAppSlot, WebAppSlotName);
        }
    }
}
