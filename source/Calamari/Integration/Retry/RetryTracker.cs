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
        readonly int maxRetries;
        readonly TimeSpan timeLimit;
        private readonly Stopwatch stopWatch = new Stopwatch();
        readonly RetryInterval retryInterval;

        public bool ThrowOnFailure { get; private set; }

        public int CurrentTry { get; private set; }
        public bool IsNotFirstAttempt => CurrentTry > 1;

        public RetryTracker(int maxRetries, TimeSpan timeLimit, RetryInterval retryInterval, bool throwOnFailure = true)
        {
            if (maxRetries < 1)
                throw new ArgumentException("maxretries must be 1 or more");
            this.maxRetries = maxRetries;
            this.timeLimit = timeLimit;
            this.retryInterval = retryInterval;
            ThrowOnFailure = throwOnFailure;
        }

        public bool Try()
        {
            var canRetry = CanRetry();
            CurrentTry++;
            return canRetry;
        }

        public TimeSpan Sleep()
        {
            return retryInterval.GetInterval(CurrentTry);
        }

        public bool CanRetry()
        {
            if (!stopWatch.IsRunning)
                stopWatch.Start();

            return CurrentTry <= maxRetries && stopWatch.Elapsed <= timeLimit;
        }

        TimeSpan nextWarning = TimeSpan.Zero;
        public bool ShouldLogWarning()
        {
            var warn = CurrentTry < 5 || (stopWatch.Elapsed > nextWarning);
            if (warn)
            {
                nextWarning = stopWatch.Elapsed.Add(TimeSpan.FromSeconds(10));
            }
            return warn;
        }
    }
}
