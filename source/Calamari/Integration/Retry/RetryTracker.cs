using System;
using System.Diagnostics;

namespace Calamari.Integration.Retry
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
        TimeSpan lastTry = TimeSpan.Zero;

        public int CurrentTry { get { return currentTry; } }

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
            var canRetry = CanRetry();
            currentTry++;
            lastTry = stopWatch.Value.Elapsed;
            return canRetry;
        }

        public int Sleep()
        {
            return retryInterval.GetInterval(currentTry);
        }

        public bool CanRetry()
        {
            bool noRetry = (maxRetries.HasValue && currentTry > maxRetries.Value) ||
                (timeLimit != null && lastTry.TotalMilliseconds + retryInterval.GetInterval(currentTry) > timeLimit.Value.TotalMilliseconds);
            return !noRetry;
        }

        TimeSpan nextWarning = TimeSpan.Zero;
        public bool ShouldLogWarning()
        {
            var warn = currentTry < 5 || (stopWatch.Value.Elapsed > nextWarning);
            if (warn)
            {
                nextWarning = stopWatch.Value.Elapsed.Add(TimeSpan.FromSeconds(10));
            }
            return warn;
        }

        public bool IsFirstAttempt { get { return currentTry == 1; } }
        public bool IsSecondAttempt { get { return currentTry == 2; } }
        public bool IsNotFirstAttempt { get { return currentTry != 1; } }
    }
}
