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
    }
}
