using System;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.Testing.Helpers
{
    public static class Eventually
    {
        public static async Task ShouldEventually(Action action, ILog log, CancellationToken cancellationToken)
        {
            Task WrappedAction(CancellationToken ct) => Task.Run(action, ct).WithCancellationToken(cancellationToken);

            await ShouldEventually(WrappedAction, log, cancellationToken);
        }

        public static async Task ShouldEventually(Func<Task> action, ILog log, CancellationToken cancellationToken)
        {
            Task WrappedAction(CancellationToken ct) => Task.Run(action, ct).WithCancellationToken(ct);
            await ShouldEventually(WrappedAction, log,  cancellationToken);
        }

        public static async Task ShouldEventually(Func<CancellationToken, Task> action, ILog log, CancellationToken cancellationToken)
        {
            var strategy = new EventuallyStrategy(log);
            await strategy.ShouldEventually(action, cancellationToken);
        }

        public static async Task ShouldEventually(Func<CancellationToken, Task> action, EventuallyStrategy.Timing timing, ILog log, CancellationToken cancellationToken)
        {
            var strategy = new EventuallyStrategy(log, timing);
            await strategy.ShouldEventually(action, cancellationToken);
        }
    }
}
