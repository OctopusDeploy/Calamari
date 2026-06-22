using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.AzureResourceGroup.Bicep
{
    /// <summary>
    /// Compiles a Bicep file to an ARM template by shelling out to the az CLI (`az bicep build`). This is the
    /// external-tool touch point of Bicep deployment; mocking it lets the template-source resolution and
    /// deploy wiring in <see cref="DeployBicepTemplateBehaviour" /> be unit-tested without the az CLI installed.
    /// </summary>
    interface IBicepCompiler
    {
        // Returns the path to the generated ARM template.
        string BuildArmTemplate(string workingDirectory, string bicepFilePath);
    }

    class BicepCompiler(ILog log, ICommandLineRunner commandLineRunner) : IBicepCompiler
    {
        public string BuildArmTemplate(string workingDirectory, string bicepFilePath)
            => new BicepCli(log, commandLineRunner, workingDirectory).BuildArmTemplate(bicepFilePath);
    }
}
