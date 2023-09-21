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
            (string type, string accountId, string authenticationMethod, TAccountDetails accountDetails)
        {
            Type = type;
            AccountId = accountId;
            AuthenticationMethod = authenticationMethod;
            AccountDetails = accountDetails;
        }

        public string Type { get; set; }

        public string AuthenticationMethod { get; }

        public string AccountId { get; set; }

        public TAccountDetails AccountDetails { get; set; }
    }
}
