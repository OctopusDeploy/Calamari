using System.IO;
using Calamari.Azure.Tests.Deployment.Azure;
using Calamari.Deployment;
using Calamari.Tests.Helpers;
using NUnit.Framework;
using Octostache;

namespace Calamari.Azure.Tests.PowerShell
{
    [TestFixture]
    [Category(TestEnvironment.CompatibleOS.Windows)]
    public class AzurePowershellFixture : CalamariFixture
    {
        [Test]
        public void ShouldSetAzureSubscription()
        {
            // If the Azure test certificate is not installed, we cannot run, so ignore
            OctopusTestAzureSubscription.IgnoreIfCertificateNotInstalled();

            var variablesFile = Path.GetTempFileName();
            var variables = new VariableDictionary();
            variables.Set(SpecialVariables.Account.AccountType, "AzureSubscription");
            variables.Set(SpecialVariables.Account.Name, "AzureTest");
            variables.Save(variablesFile);
            OctopusTestAzureSubscription.PopulateVariables(variables);

            var output = Invoke(Calamari()
                .Action("run-script")
                .Argument("script", GetFixtureResouce("AzureSubscription.ps1"))
                .Argument("variables", variablesFile));

            output.AssertZero();
            output.AssertOutput("Current subscription ID: " + OctopusTestAzureSubscription.AzureSubscriptionId);
        }
    }
}