using Calamari.Integration.ConfigurationVariables;
using Calamari.Integration.FileSystem;

namespace Calamari.Conventions
{
    public class ConfigurationVariablesConvention : IInstallConvention
    {
        readonly ICalamariFileSystem fileSystem;
        readonly IConfigurationVariablesReplacer replacer;

        public ConfigurationVariablesConvention(ICalamariFileSystem fileSystem, IConfigurationVariablesReplacer replacer)
        {
            this.fileSystem = fileSystem;
            this.replacer = replacer;
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
                replacer.ModifyConfigurationFile(configurationFile, deployment.Variables);
            }
        }
    }
}