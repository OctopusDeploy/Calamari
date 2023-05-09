using System;
using System.Diagnostics;

namespace Calamari.Kubernetes.ResourceStatus
{
    /// <summary>
    /// Represents a timer that completes the countdown after a predefined period of time once started.
    /// </summary>
    public interface ICountdownTimer
    {
        void Start();
        void Reset();
        bool HasStarted();
        bool HasCompleted();
    }
    
    /// <summary>
    /// <inheritdoc />
    /// </summary>
    public class CountdownTimer : ICountdownTimer
    {
        private readonly TimeSpan duration;
        private readonly Stopwatch stopwatch;
        
        public CountdownTimer(TimeSpan duration)
        {
            this.duration = duration;
            stopwatch = new Stopwatch();
        }

        public void Start() => stopwatch.Start();
        public void Reset() => stopwatch.Reset();
        public bool HasStarted() => stopwatch.IsRunning;
        public bool HasCompleted() => stopwatch.Elapsed > duration;
    }

    /// <summary>
    /// Represents a CountdownTimer that never completes
    /// </summary>
    public class InfiniteCountdownTimer : ICountdownTimer
    {
        private bool hasStarted;
        public void Start() => hasStarted = true;
        public void Reset() => hasStarted = false;
        public bool HasStarted() => hasStarted;
        public bool HasCompleted() => false;
    }
}