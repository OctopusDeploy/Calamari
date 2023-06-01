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

    /// <summary>
    /// <inheritdoc />
    /// </summary>
    public class Timer : ITimer
    {
        private readonly TimeSpan duration;
        private readonly TimeSpan interval;
        private readonly Stopwatch stopwatch;

        public Timer(TimeSpan duration, TimeSpan interval)
        {
            this.duration = duration;
            this.interval = interval;
            stopwatch = new Stopwatch();
        }

        public void Start() => stopwatch.Start();
        public void Restart() => stopwatch.Restart();
        public bool HasCompleted() => stopwatch.IsRunning && stopwatch.Elapsed >= duration;
        public void WaitForInterval() => Thread.Sleep(interval);
    }

    /// <summary>
    /// Represents a CountdownTimer that never completes
    /// </summary>
    public class InfiniteTimer : ITimer
    {
        private readonly TimeSpan interval;

        public InfiniteTimer(TimeSpan interval) => this.interval = interval;

        public void Start() { }
        public void Restart() { }

        public bool HasCompleted() => false;
        public void WaitForInterval() => Thread.Sleep(interval);
    }
}