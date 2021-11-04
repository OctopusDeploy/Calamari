using System;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.PackageRetention;
using Calamari.Deployment.PackageRetention.Repositories;
using Calamari.Integration.Nginx;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.TypeConverters
{
    [TestFixture]
    public class TinyTypeConverterFixture
    {
        [Test]
        public void WhenValueIsNull_ThenThrowException()
        {
              var tc = new TinyTypeTypeConverter<ServerTaskID>();
              Assert.Throws<ArgumentNullException>(() => tc.ConvertFrom(null));
        }

        [Test]
        public void WhenValueIsNonString_ThenThrowException()
        {
            var tc = new TinyTypeTypeConverter<ServerTaskID>();
            Assert.Throws<Exception>(() => tc.ConvertFrom(1));
        }

        [Test]
        public void WhenValueIsValidString_ThenProduceTinyType()
        {
            var validString = "Valid String";
            var tc = new TinyTypeTypeConverter<ServerTaskID>();
            var result = tc.ConvertFrom(validString) as ServerTaskID;
            var expectedResult = CaseInsensitiveTinyType.Create<ServerTaskID>(validString);
            Assert.AreEqual(expectedResult, result);
        }
    }
}