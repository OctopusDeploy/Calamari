using Microsoft.WindowsAzure;

namespace Calamari.Tests.Helpers.Azure
{
    public class FakeSubscriptionCloudCredentials : SubscriptionCloudCredentials
    {
        readonly string subscriptionId;

        public FakeSubscriptionCloudCredentials(string subscriptionId)
        {
            this.subscriptionId = subscriptionId;
        }

        public override string SubscriptionId { get{ return subscriptionId; } }
    }
}