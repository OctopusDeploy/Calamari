using Sashimi.Server.Contracts.Accounts;

namespace Sashimi.GCP.Accounts
{
    class GcpAccountVerifier : IVerifyAccount
    {
        public void Verify(AccountDetails account)
        {
            var accountTyped = (GcpAccountDetails) account;
            // TODO: add account verification
        }
    }
}