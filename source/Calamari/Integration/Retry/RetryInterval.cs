namespace Calamari.Integration.Retry
{
    public abstract class RetryInterval
    {
        public abstract int GetInterval(int retryCount);
    }
}