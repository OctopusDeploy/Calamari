using System;
using System.Diagnostics;

namespace Calamari.Integration.FileSystem
{
    /// <summary>
    /// Retry logic tracks when to retry vs fail and calculates sleep times for retries
    /// </summary>
    /// <remarks>
    /// while (retryLogic.Try())
    /// {  .... 
    ///    catch 
    ///    ... 
    ///      if retryLogic.CanRetry()
    ///      {
    ///        Thread.Sleep(retryLogic.Sleep())
    ///      }
    ///      else
    ///        throw;
    /// 
    /// For a file system RetryTracker, use a small fixed interval and a limit of say 1 minute
    /// For a network RetryTracker, use an exponential retry up to, say, 30 seconds to prevent spamming host
    /// </remarks>
    public class RetryTracker
    {
        readonly int? maxRetries;
        readonly TimeSpan? timeLimit;
        readonly Lazy<Stopwatch> stopWatch = new Lazy<Stopwatch>(Stopwatch.StartNew);
        readonly RetryInterval retryInterval;

        public bool ThrowOnFailure { get; private set; }

        int currentTry = 0;

        public RetryTracker(int? maxRetries, TimeSpan? timeLimit, RetryInterval retryInterval, bool throwOnFailure = true)
        {
            this.maxRetries = maxRetries;
            if (maxRetries.HasValue && maxRetries.Value < 1) throw new ArgumentException("maxretries must be 1 or more if set");
            this.timeLimit = timeLimit;
            this.retryInterval = retryInterval;
            ThrowOnFailure = throwOnFailure;
        }

        public bool Try()
        {
            currentTry++;
            return CanRetry();
        }

        public int Sleep()
        {
            return retryInterval.GetInterval(currentTry);
        }

        public bool CanRetry()
        {
            bool canRetry = !(maxRetries.HasValue && currentTry > maxRetries.Value) &&
                !(timeLimit != null && timeLimit > stopWatch.Value.Elapsed);
            return canRetry;
        }

        public bool IsFirstAttempt { get { return currentTry == 1; } }
        public bool IsSecondAttempt { get { return currentTry == 2; } }
        public bool IsNotFirstAttempt { get { return currentTry != 1; } }
    }
}
