using System;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.Variables;

namespace Sashimi.Aws.Common.Variables
{
    class AmazonWebServicesVariableTypeProvider : IVariableTypeProvider
    {
        public VariableType VariableType => AmazonWebServicesVariableType.AmazonWebServicesAccount;
        public DocumentType? DocumentType => Server.Contracts.DocumentType.Account;
    }
}