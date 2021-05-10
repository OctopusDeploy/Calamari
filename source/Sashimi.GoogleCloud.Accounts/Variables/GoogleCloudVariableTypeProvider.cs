using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.Variables;

namespace Sashimi.GoogleCloud.Accounts.Variables
{
    class GoogleCloudVariableTypeProvider : IVariableTypeProvider
    {
        public VariableType VariableType => GoogleCloudVariableType.GoogleCloudAccount;
        public DocumentType? DocumentType => Server.Contracts.DocumentType.Account;
    }
}