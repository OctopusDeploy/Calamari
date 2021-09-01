using System;
using System.Collections.Generic;

namespace Sashimi.Server.Contracts.ServiceMessages
{
    public interface IServiceMessageHandler
    {
        string AuditEntryDescription { get; }
        string ServiceMessageName { get; }
        IEnumerable<ScriptFunctionRegistration> ScriptFunctionRegistrations { get; }
    }
}