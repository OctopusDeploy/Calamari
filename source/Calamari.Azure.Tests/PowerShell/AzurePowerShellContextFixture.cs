using System.IO;
using Calamari.Azure.Integration;
using Calamari.Azure.Tests.Deployment.Azure;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Azure.Tests.PowerShell
{
    [TestFixture]
    public class AzurePowerShellContextFixture
    {
        [Test]
        public void CertificateRemovedAfterScriptExecution()
        {
            OctopusTestAzureSubscription.IgnoreIfCertificateNotInstalled();
            var powershellContext = new AzurePowerShellContext();
            var scriptEngine = Substitute.For<IScriptEngine>();
            var commandLineRunner = Substitute.For<ICommandLineRunner>();

            var variables = new CalamariVariableDictionary();
            OctopusTestAzureSubscription.PopulateVariables(variables);

            using (var variablesFile = new TemporaryFile(Path.GetTempFileName()))
            {
                var expectedCertFile = Path.Combine(variablesFile.DirectoryPath, "azure_certificate.pfx");

                scriptEngine
                    .When(engine => engine.Execute(Arg.Any<string>(), variables, commandLineRunner))
                    .Do(callInfo => Assert.True(File.Exists(expectedCertFile)));

                powershellContext.ExecuteScript(scriptEngine, variablesFile.FilePath, variables, commandLineRunner);

                Assert.False(File.Exists(expectedCertFile));
            }
        }
    }
}
