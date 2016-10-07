using System.Runtime.Serialization;

namespace Calamari.Integration.Processes.Semaphores
{
    [DataContract]
    public class FileLockContent
    {

        [DataMember]
        public long ProcessId { get; set; }

        [DataMember]
        public long Timestamp { get; set; }

        [DataMember]
        public string ProcessName { get; set; }

        [DataMember]
        public int ThreadId { get; set; }

        protected bool Equals(FileLockContent other)
        {
            return ProcessId == other.ProcessId && 
                   string.Equals(ProcessName, other.ProcessName) && 
                   ThreadId == other.ThreadId;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((FileLockContent)obj);
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

    }
}