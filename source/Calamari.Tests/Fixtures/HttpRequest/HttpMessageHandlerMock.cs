using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions.Execution;

namespace Calamari.Tests.Fixtures.HttpRequest
{
    public class HttpMessageHandlerMock : HttpMessageHandler, HttpMessageHandlerMock.IReturnOrError, HttpMessageHandlerMock.IDuration
    {
        Func<HttpRequestMessage, bool> predicate;
        HttpResponseMessage response;
        TimeSpan? duration;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (!predicate(request))
                throw new AssertionFailedException("HTTP request did not match expectation");

            if (duration.HasValue)
                Thread.Sleep(duration.Value);
            
            var tcs = new TaskCompletionSource<HttpResponseMessage>();

            if (cancellationToken.IsCancellationRequested)
            {
                tcs.TrySetCanceled();
            }
            else if (response != null)
            {
                 tcs.SetResult(response);
            }
            else
            {
               tcs.SetResult(null); 
            }

            return tcs.Task;
        }

        public IReturnOrError Expect(Func<HttpRequestMessage, bool> expectation)
        {
            predicate = expectation;
            return this;
        }

        void IDuration.Duration(TimeSpan duration)
        {
            this.duration = duration;
        }

        IDuration IReturnOrError.Return(HttpResponseMessage returns)
        {
            response = returns;
            return this;
        }

        public interface IReturnOrError 
        {
            IDuration Return(HttpResponseMessage response);
        }

        public interface IDuration
        {
            void Duration(TimeSpan duration);
        }
    }
}
    
