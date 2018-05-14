using System;

namespace Calamari.Integration.Time
{
    public interface IClock
    {
        DateTimeOffset GetUtcTime();
        DateTimeOffset GetLocalTime();
    }
}