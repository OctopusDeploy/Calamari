#nullable disable
using Octopus.Server.MessageContracts;
using Octopus.Server.MessageContracts.Attributes;
using Sashimi.Server.Contracts.Accounts;

namespace Sashimi.GCP.Accounts
{
    class GcpAccountResource : AccountDetailsResource
    {
        public override AccountType AccountType => AccountTypes.GcpAccountType;

        [Trim]
        [Writeable]
        public string ServiceAccountEmail { get; set; }

        [Trim, Writeable]
        public SensitiveValue Json { get; set; }
    }
}