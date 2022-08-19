using Newtonsoft.Json;

namespace Calamari.Common.Features.Discovery
{
    /// <summary>
    /// For account-based authentication scopes.
    /// </summary>
    public class AccountAuthenticationDetails<TAccountDetails> : ITargetDiscoveryAuthenticationDetails
    {
        [JsonConstructor]
        public AccountAuthenticationDetails
            (string type, string accountId, TAccountDetails accountDetails)
        {
            Type = type;
            AccountId = accountId;
            AccountDetails = accountDetails;
        }

        public string? Type { get; set; }

        public string AccountId { get; set; }

        public TAccountDetails AccountDetails { get; set; }
    }
}
