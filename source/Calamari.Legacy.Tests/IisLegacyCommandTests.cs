using System;
using Calamari.Legacy.Iis;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Legacy.Tests
{
    [TestFixture]
    public class IisLegacyCommandTests
    {
        [Test]
        public void GivenTwoParameters_ThenParametersPassedToOverwriteHomeDirectory()
        {
            var iisServer = Substitute.For<IInternetInformationServer>();
            var cmd = new IisLegacyCommand(iisServer);

            var websiteName = "WEBSITENAME";
            var path = "PATH";
            cmd.Execute(new[] { websiteName, path });
            iisServer.Received().OverwriteHomeDirectory(websiteName, path, false);
        }

        [Test]
        public void GivenOneParameter_ThenException()
        {
            var iisServer = Substitute.For<IInternetInformationServer>();
            var cmd = new IisLegacyCommand(iisServer);

            Assert.Throws<InvalidOperationException>(() => cmd.Execute(new string[] { Guid.NewGuid().ToString()}));
        }

        [Test]
        public void GivenNoParameter_ThenException()
        {
            var iisServer = Substitute.For<IInternetInformationServer>();
            var cmd = new IisLegacyCommand(iisServer);

            Assert.Throws<InvalidOperationException>(() => cmd.Execute(Array.Empty<string>()));
        }
    }
}