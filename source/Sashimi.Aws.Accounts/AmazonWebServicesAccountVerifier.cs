using Amazon.Runtime;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Sashimi.Server.Contracts.Accounts;

namespace Sashimi.Aws.Accounts
{
    class AmazonWebServicesAccountVerifier : IVerifyAccount
    {
        public void Verify(AccountDetails account)
        {
            var accountTyped = (AmazonWebServicesAccountDetails) account;
            using (var client = new AmazonSecurityTokenServiceClient(new BasicAWSCredentials(accountTyped.AccessKey, accountTyped.SecretKey?.Value)))
            {
                client.GetCallerIdentityAsync(new GetCallerIdentityRequest()).Wait();
            }
        }
    }
}