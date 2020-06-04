using System.Collections.Generic;
using Octostache;
using Sashimi.Server.Contracts.ServiceMessages;

namespace Sashimi.Server.Contracts.Accounts
{
    public interface IServiceMessageHandler
    {
        ServiceMessageValidationResult IsServiceMessageValid(IDictionary<string, string> messageProperties, VariableDictionary variables);
        string AuditEntryDescription { get; }
        string ServiceMessageName { get; }
    }
}