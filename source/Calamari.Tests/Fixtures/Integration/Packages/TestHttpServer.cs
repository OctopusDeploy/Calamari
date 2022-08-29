#if USE_NUGET_V2_LIBS
using System;
using System.Net;
using System.Threading;

namespace Calamari.Tests.Fixtures.Integration.Packages
{
    public class TestHttpServer : IDisposable
    {
        readonly HttpListener listener;
        
        public int Port { get; }
        public TimeSpan ResponseTime { get; }

        public string BaseUrl => $"http://localhost:{Port}";

        public TestHttpServer(int port, TimeSpan responseTime)
        {
            Port = port;
            ResponseTime = responseTime;
            listener = new HttpListener
            {
                Prefixes = { BaseUrl + "/" }
            };

            listener.Start();
            listener.BeginGetContext(OnRequest, listener);
        }

        void OnRequest(IAsyncResult result)
        {
            var context = listener.EndGetContext(result);
            Thread.Sleep(ResponseTime);
            var response = context.Response;
            response.StatusCode = 200;
            response.OutputStream.Close();
        }
        
        public void Dispose()
        {
            listener.Stop();
        }
    }
}
#endif