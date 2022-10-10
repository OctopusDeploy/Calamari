using System;
using Calamari.Commands;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Commands
{
    [TestFixture]
    public class RegisterPackageUseCommandTest
    {
        [Test]
        public void SupportsMavenVersionFormats()
        {
            var registerPackageCmd = new RegisterPackageUseCommand(Substitute.For<ILog>(), Substitute.For<IManagePackageCache>(), Substitute.For<ICalamariFileSystem>());
            registerPackageCmd.Execute(new[] { "-packageId=Blah", "-packageVersion=3.7.4.20220919T144341Z", "-packageVersionFormat=Maven", "-packagePath=C:\\Octopus" }).Should().Be(0);
        }
    }
}