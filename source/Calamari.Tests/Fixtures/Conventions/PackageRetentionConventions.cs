using System;
using System.Linq;
using Calamari.Deployment.PackageRetention.Repositories;
using Calamari.Tests.Fixtures.Conventions.ConventionSpecifications;
using Conventional;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Conventions
{
    [TestFixture]
    public class PackageRetentionConventions
    {
        [Test]
        public void MustNotCreateNewInstancesOfJsonJournalRepository()
        {
            const string reason = "JsonJournalRepository should only be provisioned by JsonJournalRepositoryFactory. We don't want unknown accesses to the repository floating around.";

            var types = AppDomainScanner.CalamariTypes
                                        .Where(t => t != typeof(JsonJournalRepositoryFactory)); // JsonJournalRepositoryFactory is allowed to create new instances of the repository

            var forbiddenTypes = new [] { typeof(JsonJournalRepository) };

            types
                .MustConformTo(new MustNotCreateNewInstancesOfConventionSpecification(forbiddenTypes, reason))
                .WithFailureAssertion(Assert.Fail);
        }
    }
}