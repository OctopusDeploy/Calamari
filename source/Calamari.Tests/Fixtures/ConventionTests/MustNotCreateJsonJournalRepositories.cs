using Calamari.Deployment.PackageRetention.Repositories;
using Conventional;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.ConventionTests
{
    [TestFixture]
    public class MustNotCreateJsonJournalRepositories
    {
        [Test]
        public void Test()
        {
            new [] { typeof(JsonJournalRepository) }
                .MustConformTo(new MustNotCreateNewInstancesOfConventionSpecification())
                .WithFailureAssertion(Assert.Fail);
        }
    }
}