using System;
using System.Collections.Generic;
using System.Linq;

namespace Sashimi.Server.Contracts.ServiceMessages
{
    public class ServiceMessageValidationResult
    {
        public static ServiceMessageValidationResult Valid = new ServiceMessageValidationResult(true, Enumerable.Empty<string>());

        public static ServiceMessageValidationResult Invalid(IEnumerable<string> messages)
        {
            return new ServiceMessageValidationResult(false, messages);
        }

        ServiceMessageValidationResult(bool isValid, IEnumerable<string> messages)
        {
            IsValid = isValid;
            Messages = messages.ToArray();
        }

        public bool IsValid { get; }
        public string[] Messages { get; }
    }
}