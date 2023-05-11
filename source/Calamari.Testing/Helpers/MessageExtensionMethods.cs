using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.ServiceMessages;

namespace Calamari.Testing.Helpers;

public static class MessageExtensionMethods
{
    public static ServiceMessage[] GetServiceMessagesOfType(this IEnumerable<InMemoryLog.Message> messages,
        string serviceMessageType)
    {
        return messages.Select(m => m.FormattedMessage)
                       .Where(m => m.StartsWith($"{ServiceMessage.ServiceMessageLabel}[{serviceMessageType}"))
                       .Select(ServiceMessage.ParseRawServiceMessage)
                       .ToArray();
    }
}