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
        TimeSpan Interval { get; set; }
        TimeSpan Duration { get; set; }
        void Start();
        void Restart();
        bool HasCompleted();
        void WaitForInterval();
    }

    public class Timer : ITimer
    {
        private readonly Stopwatch stopwatch = new Stopwatch();
        public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(1);
        public TimeSpan Duration { get; set; } = Timeout.InfiniteTimeSpan;
        public void Start() => stopwatch.Start();
        public void Restart() => stopwatch.Restart();
        public bool HasCompleted() => Duration != Timeout.InfiniteTimeSpan && stopwatch.IsRunning && stopwatch.Elapsed >= Duration;
        public void WaitForInterval() => Thread.Sleep(Interval);
    }
}