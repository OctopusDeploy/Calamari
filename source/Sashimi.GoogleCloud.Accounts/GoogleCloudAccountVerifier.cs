using Sashimi.Server.Contracts.Accounts;

namespace Sashimi.GoogleCloud.Accounts
{
    class GoogleCloudAccountVerifier : IVerifyAccount
    {
        public void Verify(AccountDetails account)
        {
            var accountTyped = (GoogleCloudAccountDetails) account;
        }
    }
}