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
    /// catch
    /// ...
    /// if retryLogic.CanRetry()
    /// {
    /// Thread.Sleep(retryLogic.Sleep())
    /// }
    /// else
    /// throw;
    /// For a file system RetryTracker, use a small fixed interval and a limit of say 1 minute
    /// For a network RetryTracker, use an exponential retry up to, say, 30 seconds to prevent spamming host
    /// </remarks>
    public class RetryTracker
    {
        readonly int? maxRetries;
        readonly TimeSpan? timeLimit;
        readonly Stopwatch stopWatch = new Stopwatch();
        readonly RetryInterval retryInterval;

        bool shortCircuit;
        TimeSpan lastTry = TimeSpan.Zero;
        TimeSpan nextWarning = TimeSpan.Zero;

        public RetryTracker(int? maxRetries, TimeSpan? timeLimit, RetryInterval retryInterval, bool throwOnFailure = true)
        {
            this.maxRetries = maxRetries;
            if (maxRetries.HasValue && maxRetries.Value < 0)
                throw new ArgumentException("maxretries must be 0 or more if set");
            this.timeLimit = timeLimit;
            this.retryInterval = retryInterval;
            ThrowOnFailure = throwOnFailure;
        }

        public bool ThrowOnFailure { get; }

        public int CurrentTry { get; set; }

        public TimeSpan TotalElapsed => stopWatch.Elapsed;

        public bool IsNotFirstAttempt => CurrentTry != 1;

        public bool Try()
        {
            stopWatch.Start();
            var canRetry = CanRetry();
            CurrentTry++;
            lastTry = stopWatch.Elapsed;
            return canRetry;
        }

        public TimeSpan Sleep()
        {
            return retryInterval.GetInterval(CurrentTry);
        }

        public bool CanRetry()
        {
            var noRetry = shortCircuit && CurrentTry > 0 ||
                maxRetries.HasValue && CurrentTry > maxRetries.Value ||
                timeLimit.HasValue && lastTry + retryInterval.GetInterval(CurrentTry) > timeLimit.Value;
            return !noRetry;
        }

        public bool ShouldLogWarning()
        {
            var warn = CurrentTry < 5 || stopWatch.Elapsed > nextWarning;
            if (warn)
                nextWarning = stopWatch.Elapsed.Add(TimeSpan.FromSeconds(10));
            return warn;
        }

        /// <summary>
        /// Resets the tracker to start from the start. Sets the ShortCircuit flag if the maximums were reached.
        /// DO NOT call from within a RetryTacker.Try loop as otherwise it will never finish
        /// </summary>
        public void Reset()
        {
            if (!CanRetry())
                shortCircuit = true;

            stopWatch.Reset();
            CurrentTry = 0;
        }
    }
}