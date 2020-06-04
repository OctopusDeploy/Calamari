using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions.Execution;

namespace Calamari.Tests.Fixtures.HttpRequest
{
    public class HttpMessageHandlerMock : HttpMessageHandler, HttpMessageHandlerMock.IExpectation
    {
        Func<HttpRequestMessage, bool> predicate;
        HttpResponseMessage response;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (!predicate(request))
                throw new AssertionFailedException("HTTP request did not match expectation");

            return Task.FromResult(response);
        }

        public IExpectation Expect(Func<HttpRequestMessage, bool> expectation)
        {
            this.predicate = expectation;
            return this;
        }


        void IExpectation.Return(HttpResponseMessage returns)
        {
            this.response = returns;
        }

        public interface IExpectation
        {
            void Return(HttpResponseMessage response);
        }
    }
}
    
