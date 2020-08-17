using System;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Util;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Util
{
    public class MockHashingAlgorithm : HashAlgorithm
    {
        public override void Initialize()
        {
        }

        protected override void HashCore(byte[] array, int ibStart, int cbSize)
        {
        }

        protected override byte[] HashFinal()
        {
            return Encoding.UTF8.GetBytes("Supercalifragilisticexpialidocious");
        }
    }

    [TestFixture]
    public class HashCalculatorFixture
    {
        public static Func<THash> CreateHashFipsFailure<THash>() where THash: HashAlgorithm
        {
            return () => throw new TargetInvocationException(new InvalidOperationException("This implementation is not part of the Windows Platform FIPS validated cryptographic algorithms."));
        }

        public static Func<THash> CreateNullHashFactory<THash>() where THash : HashAlgorithm
        {
            return () => null;
        }

        [Test]
        public void ShouldReturnFalseIfHashingAlgorithmIsNotSupported()
        {
            Assert.IsFalse(HashCalculator.IsAvailableHashingAlgorithm(CreateHashFipsFailure<MD5>()));
        }

        [Test]
        public void ShouldReturnTrueIfHashingAlgorithmIsSupported()
        {
            Assert.IsTrue(HashCalculator.IsAvailableHashingAlgorithm(() => new MockHashingAlgorithm()));
        }

        [Test]
        public void ShouldReturnFalseIfHashingAlgorithmFactoryReturnsNull()
        {
            Assert.IsFalse(HashCalculator.IsAvailableHashingAlgorithm(CreateNullHashFactory<MD5>()));
        }
    }
}
