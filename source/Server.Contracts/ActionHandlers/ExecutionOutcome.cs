using System;

namespace Sashimi.Server.Contracts.ActionHandlers
{
    public enum ExecutionOutcome
    {
        Successful = 1,
        Cancelled = 2,
        TimeOut = 3,
        Unsuccessful = 4
    }
}