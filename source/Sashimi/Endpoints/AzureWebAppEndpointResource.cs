﻿#nullable disable
using Octopus.Data.Resources.Attributes;
using Sashimi.Server.Contracts.Endpoints;

namespace Sashimi.AzureWebApp.Endpoints
{
    public class AzureWebAppEndpointResource : EndpointResource
    {
        public override CommunicationStyle CommunicationStyle => CommunicationStyle.AzureWebApp;

        [Trim]
        [Writeable]
        public string AccountId { get; set; }

        [Trim]
        [Writeable]
        public string ResourceGroupName { get; set; }

        [Trim]
        [Writeable]
        public string WebAppName { get; set; }

        [Trim]
        [Writeable]
        public string WebAppSlotName { get; set; }

        [Trim]
        [Writeable]
        public string DefaultWorkerPoolId { get; set; }
    }
}