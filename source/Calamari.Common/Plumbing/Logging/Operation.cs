using System;
using System.Collections.Generic;
using System.Diagnostics;
using Calamari.Common.Plumbing.ServiceMessages;

namespace Calamari.Common.Plumbing.Logging
{
    public class Operation : IDisposable
    {
        public bool IsAbandoned { get; private set; }
        public string OperationId { get; }

        readonly ILog logger;
        readonly Stopwatch stopwatch;
        readonly string operationName;

        bool isCompleted;
        bool isDisposed;

        public Operation(ILog logger, string operationName)
        {
            OperationId = Guid.NewGuid().ToString("n");
            this.logger = logger;
            this.operationName = operationName;
            stopwatch = Stopwatch.StartNew();
            isDisposed = false;

            logger.VerboseFormat($"Timed operation '{operationName}' started.");
        }

        public void Complete()
        {
            if (isCompleted || IsAbandoned) throw new InvalidOperationException("This logging operation has already been completed or abandoned.");
            
            logger.VerboseFormat($"Timed operation '{operationName}' completed in {stopwatch.ElapsedMilliseconds}ms.");
            LogTimedOperationServiceMessage(stopwatch.ElapsedMilliseconds, Outcome.Completed);

            isCompleted = true;
            Dispose();
        }

        void LogTimedOperationServiceMessage(long stopwatchElapsedMilliseconds, Outcome operationOutcome)
        {
            // Determine operation duration
            // Construct service message ##octopus[serviceMessage='calamari-timed-operation' name='Deployment package extraction' operationId='679d7cfadc614909a87e2005000c84c1' durationMilliseconds='1642' outcome='Completed']
            var serviceMessageParameters = new Dictionary<string, string>
            {
                { ServiceMessageNames.CalamariTimedOperation.OperationIdAttribute, OperationId },
                { ServiceMessageNames.CalamariTimedOperation.NameAttribute, operationName },
                { ServiceMessageNames.CalamariTimedOperation.DurationMillisecondsAttribute, stopwatchElapsedMilliseconds.ToString() },
                { ServiceMessageNames.CalamariTimedOperation.OutcomeAttribute, operationOutcome.ToString() },
            };

            logger.WriteServiceMessage(new ServiceMessage(ServiceMessageNames.CalamariTimedOperation.Name, serviceMessageParameters));
        }

        public void Abandon(Exception? ex = null)
        {
            if (isCompleted || IsAbandoned) throw new InvalidOperationException("This logging operation has already been completed or abandoned.");

            LogTimedOperationServiceMessage(stopwatch.ElapsedMilliseconds, Outcome.Abandoned);
            
            IsAbandoned = true;
            Dispose();
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            if (!isCompleted && !IsAbandoned)
            {
                Abandon();
            }

            isDisposed = true;
        }

        enum Outcome
        {
            Completed,
            Abandoned
        }
    }
}
