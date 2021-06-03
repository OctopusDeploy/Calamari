#nullable disable
using Octopus.Server.MessageContracts;
using Octopus.Server.MessageContracts.Attributes;
using Octopus.Server.MessageContracts.Features.Accounts;

namespace Sashimi.GoogleCloud.Accounts
{
    class GoogleCloudAccountResource : AccountResource
    {
        public override AccountType AccountType => AccountType.GoogleCloud;

        [Trim, Writeable]
        public SensitiveValue JsonKey { get; set; }
    }
}