using System;
using System.Diagnostics;

namespace Calamari.Common.Plumbing.Retry
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
        readonly Stopwatch stopWatch = new Stopwatch();
        readonly RetryInterval retryInterval;

        public bool ThrowOnFailure { get; private set; }

        int currentTry = 0;
        bool shortCircuit;
        TimeSpan lastTry = TimeSpan.Zero;
        TimeSpan nextWarning = TimeSpan.Zero;

        public int CurrentTry { get { return currentTry; } }

        public TimeSpan TotalElapsed => stopWatch.Elapsed;

        public RetryTracker(int? maxRetries, TimeSpan? timeLimit, RetryInterval retryInterval, bool throwOnFailure = true)
        {
            this.maxRetries = maxRetries;
            if (maxRetries.HasValue && maxRetries.Value < 0) throw new ArgumentException("maxretries must be 0 or more if set");
            this.timeLimit = timeLimit;
            this.retryInterval = retryInterval;
            ThrowOnFailure = throwOnFailure;
        }

        public bool Try()
        {
            stopWatch.Start();
            var canRetry = CanRetry();
            currentTry++;
            lastTry = stopWatch.Elapsed;
            return canRetry;
        }

        public TimeSpan Sleep()
        {
            return retryInterval.GetInterval(currentTry);
        }

        public bool CanRetry()
        {
            bool noRetry = (shortCircuit && currentTry > 0) ||
                           (maxRetries.HasValue && currentTry > maxRetries.Value) ||
                           (timeLimit.HasValue && (lastTry + retryInterval.GetInterval(currentTry)) > timeLimit.Value);
            return !noRetry;
        }

        public bool ShouldLogWarning()
        {
            var warn = currentTry < 5 || (stopWatch.Elapsed > nextWarning);
            if (warn)
            {
                nextWarning = stopWatch.Elapsed.Add(TimeSpan.FromSeconds(10));
            }
            return warn;
        }

        public bool IsNotFirstAttempt => currentTry != 1;

        /// <summary>
        /// Resets the tracker to start from the start. Sets the ShortCircuit flag if the maximums were reached.
        /// DO NOT call from within a RetryTacker.Try loop as otherwise it will never finish
        /// </summary>
        public void Reset()
        {
            if (!CanRetry())
                shortCircuit = true;

            stopWatch.Reset();
            currentTry = 0;
        }
    }
}
