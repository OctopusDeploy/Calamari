using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.AzureResourceGroup.Bicep
{
    public interface IBicepTemplateBuilder
    {
        // Returns the path to the generated ARM template.
        string BuildArmTemplate(string workingDirectory, string bicepFilePath);
    }

    class BicepBuilder(ILog log, ICommandLineRunner commandLineRunner) : IBicepTemplateBuilder
    {
        public string BuildArmTemplate(string workingDirectory, string bicepFilePath)
            => new BicepCli(log, commandLineRunner, workingDirectory).BuildArmTemplate(bicepFilePath);
    }
}
