using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.Variables;

namespace Sashimi.GCP.Accounts.Variables
{
    class GcpVariableTypeProvider : IVariableTypeProvider
    {
        public VariableType VariableType => GcpVariableType.GcpAccount;
        public DocumentType? DocumentType => Server.Contracts.DocumentType.Account;
    }
}