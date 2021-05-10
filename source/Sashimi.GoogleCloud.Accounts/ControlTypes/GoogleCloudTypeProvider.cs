using Sashimi.GoogleCloud.Accounts.Variables;
using Sashimi.Server.Contracts.Actions.Templates;
using Sashimi.Server.Contracts.Variables;

namespace Sashimi.GoogleCloud.Accounts.ControlTypes
{
    class GoogleCloudControlTypeProvider : IControlTypeProvider
    {
        public ControlType ControlType => GoogleCloudControlType.GoogleCloudAccount;
        public VariableType VariableType => GoogleCloudVariableType.GoogleCloudAccount;
    }
}