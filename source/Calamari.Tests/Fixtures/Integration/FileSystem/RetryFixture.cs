using System;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Retry;
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
            var subject = new RetryTracker(100, TimeSpan.FromMinutes(1), new RetryInterval(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(200)));
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
    }
}