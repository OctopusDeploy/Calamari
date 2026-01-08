using System;
using System.Threading;

namespace Calamari.AzureResourceGroup.Tests;

public abstract class CalamariTest
{
    readonly CancellationTokenSource cancellationTokenSource;
    protected CancellationToken CancellationToken => cancellationTokenSource.Token;

    protected virtual TimeSpan DefaultTestTimeout => TimeSpan.MaxValue;

    protected CalamariTest()
    {
        var ctsTimeout = new CancellationTokenSource(DefaultTestTimeout);
        cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(ctsTimeout.Token, TestContext.Current.CancellationToken);
    }
}