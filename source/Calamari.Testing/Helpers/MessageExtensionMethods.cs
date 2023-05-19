using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.ServiceMessages;

namespace Calamari.Testing.Helpers;

public static class MessageExtensionMethods
{
    public static ServiceMessage[] GetServiceMessagesOfType(this IEnumerable<InMemoryLog.Message> messages,
        string serviceMessageType)
    {
        return messages.Where(m => m.FormattedMessage.StartsWith($"{ServiceMessage.ServiceMessageLabel}[{serviceMessageType}"))
                       .Select(m => m.ParseRawServiceMessage())
                       .ToArray();
    }

    static ServiceMessage ParseRawServiceMessage(this InMemoryLog.Message message)
    {
        var serviceMessageLog = message.FormattedMessage;
        serviceMessageLog = serviceMessageLog.Split('[')[1].Split(']')[0];
        var parts = serviceMessageLog.Split(' ');
        var serviceMessageType = parts[0];
        var properties = parts.Skip(1).Select(s =>
        {
            var key = s.Substring(0, s.IndexOf('='));
            var value = s.Substring(s.IndexOf('=') + 1).Trim('\'', '\"');
            return (Key: key, Value: value);
        });
        return new ServiceMessage(serviceMessageType,
            properties.ToDictionary(x => x.Key, x => AbstractLog.UnconvertServiceMessageValue(x.Value)));
    }
}