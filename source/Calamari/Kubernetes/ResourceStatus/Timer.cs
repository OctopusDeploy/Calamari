#if !NET40
using System;
using System.Diagnostics;
using System.Threading;

namespace Calamari.Kubernetes.ResourceStatus
{
    /// <summary>
    /// Represents a timer that completes the countdown after a predefined period of time once started.
    /// </summary>
    public interface ITimer
    {
        void Start();
        void Restart();
        bool HasCompleted();
        void WaitForInterval();
    }

    public class Timer : ITimer
    {
        public delegate ITimer Factory(TimeSpan interval, TimeSpan duration);

        private readonly Stopwatch stopwatch = new Stopwatch();
        private readonly TimeSpan interval;
        private readonly TimeSpan duration;

        public Timer(TimeSpan interval, TimeSpan duration)
        {
            this.interval = interval;
            this.duration = duration;
        }
        public void Start() => stopwatch.Start();
        public void Restart() => stopwatch.Restart();
        public bool HasCompleted() => duration != Timeout.InfiniteTimeSpan && stopwatch.IsRunning && stopwatch.Elapsed >= duration;
        public void WaitForInterval() => Thread.Sleep(interval);
    }
}
#endif