using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.Logging;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Calamari.Testing.Helpers
{
    public class EventuallyStrategy
    {
        public enum Backoff
        {
            None,
            Linear,
            Exponential
        }

        static readonly TimeSpan TimeBetweenLoggingEachTransientFailure = TimeSpan.FromSeconds(15);

        readonly Timing timing;
        readonly ILog logger;
        readonly Stopwatch stopwatch = new();
        TimeSpan elapsedTimeAtLastTransientFailure = TimeSpan.Zero;

        public EventuallyStrategy(ILog logger) : this(logger, Timing.Default)
        {
        }

        public EventuallyStrategy(ILog logger, Timing timing)
        {
            this.logger = logger;
            this.timing = timing;
        }

        public EventuallyStrategy WithTiming(Timing newTiming) => new(logger, newTiming);

        public async Task ShouldEventually(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
        {
            stopwatch.Start(); // For logging only; not for retry/back-off logic.

            var delay = timing.MinDelay;
            Exception? exception = null;

            while (true)
                try
                {
                    await action(cancellationToken);
                    LogSuccess();
                    return;
                }
                catch (EventualAssertionPermanentlyFailedException ex)
                {
                    LogPermanentFailure(ex);
                    throw;
                }
                // If the exception thrown matches CancellationToken.None, this means the exception's CancellationToken has a null source
                // This means it is likely thrown via the calling code via throw new OperationCanceledException(), and not a result of the cancellationToken triggering
                // If that is the case, we want it to pass through to the transient handling in catch(Exception)
                catch (OperationCanceledException ex) when (ex.CancellationToken != CancellationToken.None && ex.CancellationToken == cancellationToken)
                {
                    var exceptionToThrow = exception ?? ex;
                    LogPermanentFailure(exceptionToThrow);
                    ExceptionDispatchInfo.Capture(exceptionToThrow).Throw(); // Throw the previous assertion failure exception if there was one; otherwise it's just the OperationCanceledException.
                }
                catch (Exception ex)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        LogPermanentFailure(ex);
                        throw;
                    }

                    LogTransientFailure(ex);
                    exception = ex; // Hold onto this exception so that if the next time around we just time out then we can rethrow the actual assertion-failure one.

                    try
                    {
                        await Task.Delay(delay, cancellationToken);

                        delay = timing.Backoff switch
                        {
                            Backoff.None => delay,
                            Backoff.Linear => delay + timing.MinDelay,
                            Backoff.Exponential => delay + delay,
                            _ => throw new ArgumentException($"Unexpected backoff value of {timing.Backoff}")
                        };

                        if (delay > timing.MaxDelay) delay = timing.MaxDelay;
                    }
                    catch (OperationCanceledException)
                    {
                        LogPermanentFailure(exception);
                        ExceptionDispatchInfo.Capture(exception).Throw();
                    }
                }
        }

        void LogSuccess()
        {
            var elapsed = stopwatch.Elapsed;
            logger.Info($"Eventual assertion succeeded after {elapsed} {elapsed.TotalMilliseconds}");
        }

        void LogTransientFailure(Exception exception)
        {
            var elapsed = stopwatch.Elapsed;
            if (elapsed - elapsedTimeAtLastTransientFailure > TimeBetweenLoggingEachTransientFailure)
            {
                elapsedTimeAtLastTransientFailure = elapsed;
                logger.Verbose(
                    $"Eventual assertion failed after {elapsed} {elapsed.TotalMilliseconds} with message {exception.Message}. Will retry if there is time. Error: {exception.Message}");
            }
        }

        void LogPermanentFailure(Exception exception)
        {
            var elapsed = stopwatch.Elapsed;
            logger.ErrorFormat("Eventual assertion failed after {0} {1} with message {2}. {3}", elapsed, elapsed.TotalMilliseconds, exception.Message, exception);
        }

        public record struct Timing(TimeSpan MinDelay, TimeSpan MaxDelay, Backoff Backoff)
        {
            /// <summary>
            /// Default timing behaviour which is 1 second retry, with exponential backoff (double each time) up to 5 seconds
            /// </summary>
            public static Timing Default { get; } = new(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), Backoff.Exponential);

            /// <summary>
            /// Timing behaviour with no delay between retries
            /// </summary>
            public static Timing NoDelay { get; } = new(TimeSpan.Zero, TimeSpan.Zero, Backoff.None);

            public static Timing Fast { get; } = new(TimeSpan.FromSeconds(0.25), TimeSpan.FromSeconds(2), Backoff.Linear);
        }
    }
}
