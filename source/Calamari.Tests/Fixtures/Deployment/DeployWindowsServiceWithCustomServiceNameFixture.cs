using Calamari.Deployment;
using Calamari.Testing.Helpers;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Deployment
{
    [TestFixture]
    [Category(TestCategory.CompatibleOS.OnlyWindows)]
    public class DeployWindowsServiceWithCustomServiceNameFixture : DeployWindowsServiceAbstractFixture
    {
        protected override string ServiceName => @"[f`o]o$b'[a]r";

        [Test]
        public void ShouldEscapeBackslashesAndDollarSignsInArgumentsPassedToScExe()
        {
            Variables[SpecialVariables.Action.WindowsService.Arguments] = @"""c:\foo $dr bar\"" ArgumentWithoutSpace";
            Variables["Octopus.Action.WindowsService.DisplayName"] = @"""c:\foo $dr bar\"" ArgumentWithoutSpace";
            Variables["Octopus.Action.WindowsService.Description"] = @"""c:\foo $dr bar\"" ArgumentWithoutSpace";
            RunDeployment();
        }

        [Test]
        public void ShouldUpdateExistingServiceWithFunnyCharactersInServiceName()
        {
            RunDeployment();
            
            //Simulate an update
            RunDeployment();
        }
    }
}