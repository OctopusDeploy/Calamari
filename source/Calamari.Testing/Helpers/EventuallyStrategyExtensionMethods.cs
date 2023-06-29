using System;
using System.Threading;
using System.Threading.Tasks;

namespace Calamari.Testing.Helpers
{
    public static class EventuallyStrategyExtensionMethods
    {
        public static async Task ShouldEventually(this EventuallyStrategy strategy, Action action, CancellationToken cancellationToken)
        {
            Task WrappedAction(CancellationToken ct) => Task.Run(action, ct).WithCancellationToken(ct);
            await strategy.ShouldEventually(WrappedAction, cancellationToken);
        }

        public static async Task ShouldEventually(this EventuallyStrategy strategy, Func<Task> action, CancellationToken cancellationToken)
        {
            Task WrappedAction(CancellationToken ct) => Task.Run(action, ct).WithCancellationToken(ct);
            await strategy.ShouldEventually(WrappedAction, cancellationToken);
        }
    }
}
