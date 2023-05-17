using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.ServiceMessages;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Plumbing.Logging
{
    public class RedactedValuesLogger : ILog
    {
        readonly ILog log;
        readonly Dictionary<string, string> redactionMap = new Dictionary<string, string>();

        public RedactedValuesLogger(ILog innerLogger)
        {
            log = innerLogger;
        }

        public void AddValueToRedact(string value, string replacement)
        {
            redactionMap[value] = replacement;
        }

        string ProcessRedactions(string rawMessage)
        {
            return redactionMap.Aggregate(rawMessage, (current, pair) => current.Replace(pair.Key, pair.Value));
        }

        public void Verbose(string message)
        {
            log.Verbose(ProcessRedactions(message));
        }

        public void VerboseFormat(string messageFormat, params object[] args)
        {
            log.VerboseFormat(ProcessRedactions(messageFormat), args);
        }

        public void Info(string message)
        {
            log.Info(ProcessRedactions(message));
        }

        public void InfoFormat(string messageFormat, params object[] args)
        {
            log.InfoFormat(ProcessRedactions(messageFormat), args);
        }

        public void Warn(string message)
        {
            log.Warn(ProcessRedactions(message));
        }

        public void WarnFormat(string messageFormat, params object[] args)
        {
            log.WarnFormat(ProcessRedactions(messageFormat), args);
        }

        public void Error(string message)
        {
            log.Error(ProcessRedactions(message));
        }

        public void ErrorFormat(string messageFormat, params object[] args)
        {
            log.ErrorFormat(ProcessRedactions(messageFormat), args);
        }

        public void SetOutputVariableButDoNotAddToVariables(string name, string value, bool isSensitive = false)
        {
            log.SetOutputVariableButDoNotAddToVariables(name, value, isSensitive);
        }

        public void SetOutputVariable(string name, string value, IVariables variables, bool isSensitive = false)
        {
            log.SetOutputVariable(name, value, variables, isSensitive);
        }

        public void NewOctopusArtifact(string fullPath, string name, long fileLength)
        {
            log.NewOctopusArtifact(fullPath, name, fileLength);
        }

        public void Progress(int percentage, string message)
        {
            log.Progress(percentage, message);
        }

        public void DeltaVerification(string remotePath, string hash, long size)
        {
            log.DeltaVerification(remotePath, hash, size);
        }

        public void DeltaVerificationError(string error)
        {
            log.DeltaVerificationError(ProcessRedactions(error));
        }

        public string FormatLink(string uri, string description = null)
        {
            return log.FormatLink(uri, description);
        }

        public void WriteServiceMessage(ServiceMessage serviceMessage)
        {
            log.WriteServiceMessage(serviceMessage);
        }
    }
}
