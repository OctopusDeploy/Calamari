#nullable disable
using Octopus.Data.Resources;
using Octopus.Data.Resources.Attributes;
using Sashimi.Server.Contracts.Accounts;

namespace Sashimi.Aws.Accounts
{
    class AmazonWebServicesAccountResource : AccountDetailsResource
    {
        public override AccountType AccountType => AccountTypes.AmazonWebServicesAccountType;

        [Trim]
        [Writeable]
        public string AccessKey { get; set; }

        [Trim, Writeable]
        public SensitiveValue SecretKey { get; set; }
    }
}