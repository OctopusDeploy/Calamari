using System;
using Sashimi.Server.Contracts.Accounts;

namespace Sashimi.AzureCloudService
{
    class AzureSubscriptionAccountVerifier : IVerifyAccount
    {
        public void Verify(AccountDetails account)
        {
            throw new Exception("Azure Management Certificates can no longer be verified through Octopus. Azure Management Certificates are being deprecated and Azure Service Principals are now recommended for authentication.");
        }
    }
}