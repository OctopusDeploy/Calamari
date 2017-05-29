using System;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using Calamari.Util;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.Fixtures.Deployment
{
    [TestFixture]
    public class AesEncryptionFixture
    {
        [Test]
        public void CanGetRandomStrings() 
        {
            var randomString1 = AesEncryption.RandomString(8);
            var randomString2 = AesEncryption.RandomString(8);

            Assert.That(randomString1, Is.Not.EqualTo(randomString2));
        }
    }
}