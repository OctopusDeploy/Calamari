using System;
using Sashimi.Server.Contracts.Actions.Templates;

namespace Sashimi.Azure.Common.ControlTypes
{
    public static class AzureControlType
    {
        public static readonly ControlType AzureServicePrincipal = new("AzureAccount");
    }
}