using System;

namespace Calamari.Integration.Time
{
    public class SystemClock : IClock
    {
        public DateTimeOffset GetUtcTime()
        {
            return DateTimeOffset.UtcNow;
        }

        public DateTimeOffset GetLocalTime()
        {
            return DateTimeOffset.Now;
        }
    }
}