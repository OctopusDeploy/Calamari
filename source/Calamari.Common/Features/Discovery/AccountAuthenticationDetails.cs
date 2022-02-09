namespace Calamari.Common.Features.Discovery
{
    /// <summary>
    /// For account-based authentication scopes.
    /// </summary>
    public class AccountAuthenticationDetails<TAccountDetails> : ITargetDiscoveryAuthenticationDetails

    {
        public AccountAuthenticationDetails
            (string accountId, TAccountDetails accountDetails)
        {
            this.AccountId = accountId;
            this.AccountDetails = accountDetails;
        }

        public string AccountId { get; set; }

        public TAccountDetails AccountDetails { get; set; }
    }
}
