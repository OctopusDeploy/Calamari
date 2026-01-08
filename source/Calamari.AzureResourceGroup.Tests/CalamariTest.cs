using System;
using System.Threading;

namespace Calamari.AzureResourceGroup.Tests;

public abstract class CalamariTest
{
    readonly CancellationTokenSource cancellationTokenSource;
    protected CancellationToken CancellationToken => cancellationTokenSource.Token;

    protected virtual TimeSpan TestTimeout => TimeSpan.FromMilliseconds(int.MaxValue);

    protected CalamariTest()
    {
        var ctsTimeout = new CancellationTokenSource(TestTimeout);
        cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(ctsTimeout.Token, TestContext.Current.CancellationToken);
    }
}