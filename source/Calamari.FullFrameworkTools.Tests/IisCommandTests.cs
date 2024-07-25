using System;
using Calamari.FullFrameworkTools.Iis;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.FullFrameworkTools.Tests
{
    [TestFixture]
    public class IisCommandTests
    {
        [Test]
        public void GivenTwoParameters_ThenParametersPassedToOverwriteHomeDirectory()
        {
            var iisServer = Substitute.For<IInternetInformationServer>();
            var cmd = new IisCommand(iisServer);

            var websiteName = "WEBSITENAME";
            var path = "PATH";
            cmd.Execute(new[] { websiteName, path });
            iisServer.Received().OverwriteHomeDirectory(websiteName, path, false);
        }

        [Test]
        public void GivenOneParameter_ThenException()
        {
            var iisServer = Substitute.For<IInternetInformationServer>();
            var cmd = new IisCommand(iisServer);

            Assert.Throws<InvalidOperationException>(() => cmd.Execute(new string[] { Guid.NewGuid().ToString()}));
        }

        [Test]
        public void GivenNoParameter_ThenException()
        {
            var iisServer = Substitute.For<IInternetInformationServer>();
            var cmd = new IisCommand(iisServer);

            Assert.Throws<InvalidOperationException>(() => cmd.Execute(Array.Empty<string>()));
        }
    }
}