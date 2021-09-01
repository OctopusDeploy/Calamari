using System;
using Sashimi.Server.Contracts.Variables;

namespace Sashimi.Azure.Common.Variables
{
    public static class AzureVariableType
    {
        public static readonly VariableType AzureServicePrincipal = new("AzureAccount");
    }
}