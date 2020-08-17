using System;
using Calamari.Common.Plumbing.Retry;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.FileSystem
{
    [TestFixture]
    public class RetryFixture
    {
        [Test]
        public void ShouldThrowOnceRetriesExceeded()
        {
            const int retries = 100;
            var subject = new RetryTracker(100, null, new LimitedExponentialRetryInterval(100, 200, 2));
            Exception caught = null;
            var retried = 0;

            try
            {
                while (subject.Try())
                {
                    try
                    {
                        throw new Exception("Blah");
                    }
                    catch
                    {
                        if (subject.CanRetry())
                        {
                            //swallow exception
                            retried++;
                        }
                        else
                        {
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                caught = ex;
            }

            Assert.NotNull(caught);
            Assert.AreEqual(retries, retried);
        }

        [Test]
        public void ShouldTryOnceAfterMaxIsReachedAndResetting()
        {
            var cnt = 0;
            var subject = new RetryTracker(20, null, new LinearRetryInterval(TimeSpan.Zero));
            while (subject.Try())
                cnt++;

            cnt.Should().Be(21);
            
            subject.Reset();

            cnt = 0;
            while (subject.Try())
                cnt++;

            cnt.Should().Be(1);
        }
        
        [Test]
        public void ShouldTryToLimitAfterMaxIsNotReachedAndResetting()
        {
            var cnt = 0;
            var subject = new RetryTracker(20, null, new LinearRetryInterval(TimeSpan.Zero));
            subject.Try();
            
            subject.Reset();

            cnt = 0;
            while (subject.Try())
                cnt++;

            cnt.Should().Be(21);
        }
    }
}