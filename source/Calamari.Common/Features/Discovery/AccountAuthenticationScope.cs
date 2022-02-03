namespace Calamari.Common.Features.Discovery
{
    /// <summary>
    /// For account-based authentication scopes.
    /// </summary>
    public class AccountAuthenticationScope<TAccountDetails> : ITargetDiscoveryAuthenticationScope

    {
        public AccountAuthenticationScope
            (string accountId, TAccountDetails accountDetails)
        {
            this.AccountId = accountId;
            this.AccountDetails = accountDetails;
        }

        public string AccountId { get; set; }

        public TAccountDetails AccountDetails { get; set; }
    }
}
