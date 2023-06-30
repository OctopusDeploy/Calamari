using System;
using System.Net.Http;

#if NETSTANDARD || NETCORE
namespace Calamari.Testing;

public sealed class TestHttpClientFactory : IHttpClientFactory, IDisposable
{
    readonly Lazy<HttpMessageHandler> lazyHandler = new(() => new HttpClientHandler());
    
    public HttpClient CreateClient(string name) => new(lazyHandler.Value, disposeHandler: false);

    public void Dispose()
    {
        if (lazyHandler.IsValueCreated)
        {
            lazyHandler.Value.Dispose();
        }
    }
}

#endif