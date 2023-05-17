using System;
using System.Collections.Generic;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.ServiceMessages;

namespace Calamari.Common.Plumbing.Extensions
{
    public static class LogExtensions
    {
        public static Operation BeginTimedOperation(this ILog log, string operationMessage)
        {
            return new Operation(log, operationMessage);
        }

        public static void LogMetric(this ILog log, string metricName, object metricValue, string? operationId = null)
        {
            var serviceMessageParameters = new Dictionary<string, string>
            {
                { ServiceMessageNames.CalamariDeploymentMetric.OperationIdAttribute, operationId },
                { ServiceMessageNames.CalamariDeploymentMetric.MetricAttribute, metricName },
                { ServiceMessageNames.CalamariDeploymentMetric.ValueAttribute, metricValue.ToString() },
            };

            log.WriteServiceMessage(new ServiceMessage(ServiceMessageNames.CalamariDeploymentMetric.Name, serviceMessageParameters));
        }

        public static void AddValueToRedact(this ILog log, string value, string replacement)
        {
            if (!(log is RedactedValuesLogger redactedValuesLogger))
            {
                // The RedactedValuesLogger is now the base ILog registration so that we can be sure
                // that logs are redacted correctly when required.
                // See: CalamariFlavourProgramAsync.cs:38 and CalamariFlavourProgram.cs:34
                // If you've made it here then you'll need to check the registrations/injection of
                // ILog to ensure that RedactedValuesLogger is injected (or adjust how this is handled).
                throw new InvalidOperationException(
                    "Unable to add values to redact because logger is not capable of redacting values.");
            }

            redactedValuesLogger.AddValueToRedact(value, replacement);
        }
    }
}
