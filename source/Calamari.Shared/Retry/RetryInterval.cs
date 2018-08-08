namespace Calamari.Shared.Retry
{
    public abstract class RetryInterval
    {
        public abstract int GetInterval(int retryCount);
    }
}