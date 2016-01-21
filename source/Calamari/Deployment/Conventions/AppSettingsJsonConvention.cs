using System.IO;
using Calamari.Integration.AppSettingsJson;
using Calamari.Integration.ConfigurationVariables;
using Calamari.Integration.FileSystem;

namespace Calamari.Deployment.Conventions
{
    public class AppSettingsJsonConvention : IInstallConvention
    {
        readonly IAppSettingsJsonGenerator appSettings;

        public AppSettingsJsonConvention(IAppSettingsJsonGenerator appSettings)
        {
            this.appSettings = appSettings;
        }

        public void Install(RunningDeployment deployment)
        {
            if (deployment.Variables.GetFlag(SpecialVariables.Package.GenerateAppSettingsJson) == false)
            {
                return;
            }

            var relativePath = deployment.Variables.Get(SpecialVariables.Package.AppSettingsJsonPath);
            if (string.IsNullOrWhiteSpace(relativePath))
                return;

            var absolutePath = Path.Combine(deployment.CurrentDirectory, relativePath);
            Log.Info($"Applying JSON configuration generation to \"{absolutePath}\"");
            appSettings.Generate(absolutePath, deployment.Variables);
        }
    }
}