using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Variables;
using Calamari.Common.Util;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Util
{
    [TestFixture]
    public class NegatingExtractionCheckerFixture
    {
        IExtractionChecker inner;
        NegatingExtractionChecker sut;
        RunningDeployment deployment;

        [SetUp]
        public void SetUp()
        {
            inner = Substitute.For<IExtractionChecker>();
            sut = new NegatingExtractionChecker(inner);
            deployment = new RunningDeployment(new CalamariVariables());
        }

        [Test]
        public void ReturnsFalseWhenInnerReturnsTrue()
        {
            inner.ShouldExtractReference(deployment, "ref").Returns(true);

            sut.ShouldExtractReference(deployment, "ref").Should().BeFalse();
        }

        [Test]
        public void ReturnsTrueWhenInnerReturnsFalse()
        {
            inner.ShouldExtractReference(deployment, "ref").Returns(false);

            sut.ShouldExtractReference(deployment, "ref").Should().BeTrue();
        }
    }
}
