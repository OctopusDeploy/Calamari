using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Calamari.Testing.Helpers
{
    [DebuggerStepThrough]
    public static class TaskExtensionMethods
    {
        public static async Task WithCancellationToken(this Task task, CancellationToken cancellationToken)
        {
            var doerTask = task;

            var thrower = new TaskCompletionSource<object>();
            using (cancellationToken.Register(tcs => ((TaskCompletionSource<object>)tcs).SetResult(new object()), thrower))
            {
                var throwerTask = thrower.Task;

                if (doerTask != await Task.WhenAny(doerTask, throwerTask))
                {
                    throw new OperationCanceledException(cancellationToken);
                }

                await doerTask;
            }
        }
    }
}
