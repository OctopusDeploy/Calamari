using Microsoft.WindowsAzure;

namespace Calamari.Azure.Tests
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