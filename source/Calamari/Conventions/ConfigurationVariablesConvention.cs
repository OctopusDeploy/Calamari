using System.Runtime.InteropServices;
using Calamari.Integration.ConfigurationVariables;
using Calamari.Integration.FileSystem;

namespace Calamari.Conventions
{
    public class ConfigurationVariablesConvention : IInstallConvention
    {
        readonly ICalamariFileSystem fileSystem;

        public ConfigurationVariablesConvention(ICalamariFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        public void Install(RunningDeployment deployment)
        {
            if (deployment.Variables.GetFlag(SpecialVariables.Package.AutomaticallyUpdateAppSettingsAndConnectionStrings) == false)
            {
                return;
            }

            Log.Verbose("Looking for appSettings and connectionStrings in any .config files");
            var configurationFiles = fileSystem.EnumerateFilesRecursively(deployment.CurrentDirectory, "*.config");
            foreach (var configurationFile in configurationFiles)
            {
                new ConfigurationVariablesReplacer().ModifyConfigurationFile(configurationFile, deployment.Variables);
            }
        }
    }
}