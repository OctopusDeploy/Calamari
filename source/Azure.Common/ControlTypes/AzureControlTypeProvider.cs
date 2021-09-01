using System;
using Sashimi.Azure.Common.Variables;
using Sashimi.Server.Contracts.Actions.Templates;
using Sashimi.Server.Contracts.Variables;

namespace Sashimi.Azure.Common.ControlTypes
{
    class AzureControlTypeProvider : IControlTypeProvider
    {
        public ControlType ControlType => AzureControlType.AzureServicePrincipal;
        public VariableType VariableType => AzureVariableType.AzureServicePrincipal;
    }
}