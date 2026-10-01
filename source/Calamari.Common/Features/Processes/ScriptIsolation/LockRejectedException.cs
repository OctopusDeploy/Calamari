using System;
using System.Diagnostics.CodeAnalysis;
using Polly.Timeout;

namespace Calamari.Common.Features.Processes.ScriptIsolation;

public sealed class LockRejectedException(string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public LockRejectedException(Exception innerException) : this("Lock acquisition failed", innerException)
    {
    }

    [DoesNotReturn]
    public static void Throw(Exception innerException)
    {
        if (innerException is LockRejectedException lockRejectedException)
        {
            throw lockRejectedException;
        }

        if (innerException is TimeoutRejectedException timeoutRejectedException)
        {
            var message = $"Lock acquisition failed after {timeoutRejectedException.Timeout}";
            throw new LockRejectedException(message, timeoutRejectedException);
        }

        throw new LockRejectedException("Lock acquisition failed", innerException);
    }
}
