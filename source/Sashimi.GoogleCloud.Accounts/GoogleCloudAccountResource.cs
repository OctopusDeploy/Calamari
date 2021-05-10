#nullable disable
using Octopus.Server.MessageContracts;
using Octopus.Server.MessageContracts.Attributes;
using Sashimi.Server.Contracts.Accounts;

namespace Sashimi.GoogleCloud.Accounts
{
    class GoogleCloudAccountResource : AccountDetailsResource
    {
        public override AccountType AccountType => AccountTypes.GoogleCloudAccountType;

        [Trim]
        [Writeable]
        public string ServiceAccountEmail { get; set; }

        [Trim, Writeable]
        public SensitiveValue JsonKey { get; set; }
    }
}