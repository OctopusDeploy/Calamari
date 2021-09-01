using System;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.Variables;

namespace Sashimi.Azure.Common.Variables
{
    class AzureVariableTypeProvider : IVariableTypeProvider
    {
        public VariableType VariableType => AzureVariableType.AzureServicePrincipal;
        public DocumentType? DocumentType => Server.Contracts.DocumentType.Account;
    }
}