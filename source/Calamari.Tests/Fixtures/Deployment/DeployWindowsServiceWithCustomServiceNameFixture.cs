using Calamari.Deployment;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Deployment
{
    [TestFixture]
    [Category(TestEnvironment.CompatibleOS.Windows)]
    public class DeployWindowsServiceWithCustomServiceNameFixture : DeployWindowsServiceAbstractFixture
    {
        protected override string ServiceName => @"foo$bar";

        [Test]
        public void ShouldEscapeBackslashesAndDollarSignsInArgumentsPassedToScExe()
        {
            Variables[SpecialVariables.Action.WindowsService.Arguments] = @"""c:\foo $dr bar\"" ArgumentWithoutSpace";
            Variables["Octopus.Action.WindowsService.DisplayName"] = @"""c:\foo $dr bar\"" ArgumentWithoutSpace";
            Variables["Octopus.Action.WindowsService.Description"] = @"""c:\foo $dr bar\"" ArgumentWithoutSpace";
            RunDeployment();
        }
    }
}