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
    public static void Throw(Exception innerException, LockFile? lockFile = null)
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

        // Not lock contention: something stopped us opening the lock file at all (e.g. a
        // missing directory or a permissions problem). Surface the cause and the path so
        // users can fix it without needing the inner exception.
        var location = lockFile is null ? "" : $" for lock file '{lockFile.File.FullName}'";
        throw new LockRejectedException($"Lock acquisition failed{location}: {innerException.Message}", innerException);
    }
}
