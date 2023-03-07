using System;
using System.Diagnostics;

namespace Calamari.Kubernetes.ResourceStatus
{
    public interface ICountdownTimer
    {
        void Start();
        void Reset();
        bool HasStarted();
        bool HasCompleted();
    }
    
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
        public bool HasCompleted() => stopwatch.Elapsed >= duration;
    }
}