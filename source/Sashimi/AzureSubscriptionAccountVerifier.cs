using System;
using System.Threading;
using System.Threading.Tasks;
using Sashimi.Server.Contracts.Accounts;

namespace Sashimi.AzureCloudService
{
    class AzureSubscriptionAccountVerifier : IVerifyAccount
    {
        public Task Verify(AccountDetails account, CancellationToken cancellationToken)
        {
            throw new Exception("Azure Management Certificates can no longer be verified through Octopus. Azure Management Certificates are being deprecated and Azure Service Principals are now recommended for authentication.");
        }
    }
}