using Sashimi.GCP.Accounts.Variables;
using Sashimi.Server.Contracts.Actions.Templates;
using Sashimi.Server.Contracts.Variables;

namespace Sashimi.GCP.Accounts.ControlTypes
{
    class GcpControlTypeProvider : IControlTypeProvider
    {
        public ControlType ControlType => GcpControlType.GcpAccount;
        public VariableType VariableType => GcpVariableType.GcpAccount;
    }
}