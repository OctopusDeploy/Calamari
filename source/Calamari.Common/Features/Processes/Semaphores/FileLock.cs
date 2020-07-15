using System;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Threading;

namespace Calamari.Common.Features.Processes.Semaphores
{
    public interface IFileLock
    {}
    
    [DataContract]
    public class FileLock : IFileLock
    {
        public FileLock(long processId, string processName, int threadId, long timestamp)
        {
            ProcessId = processId;
            ProcessName = processName;
            ThreadId = threadId;
            Timestamp = timestamp;
        }

        [DataMember]
        public long ProcessId { get; }

        [DataMember]
        public long Timestamp { get; internal set; }

        [DataMember]
        public string ProcessName { get; }

        [DataMember]
        public int ThreadId { get; }

        protected bool Equals(FileLock other)
        {
            return ProcessId == other.ProcessId &&
                string.Equals(ProcessName, other.ProcessName) &&
                ThreadId == other.ThreadId;
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != GetType())
                return false;
            return Equals((FileLock)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = ProcessId.GetHashCode();
                hashCode = (hashCode * 397) ^ (ProcessName?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ ThreadId;
                return hashCode;
            }
        }

        public bool BelongsToCurrentProcessAndThread()
        {
            return ProcessId == Process.GetCurrentProcess().Id && ThreadId == Thread.CurrentThread.ManagedThreadId;
        }
    }
}