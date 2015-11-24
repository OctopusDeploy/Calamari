using System.IO;
using Calamari.Azure.Deployment.Conventions;
using Calamari.Commands;
using Calamari.Integration.Processes;
using NUnit.Framework;

namespace Calamari.Azure.Tests
{
    [SetUpFixture]
    public class SetUpFixture
    {
        [SetUp]
        public void AssertConfigurationFilesExist()
        {
            var calamariFullPath = typeof(DeployPackageCommand).Assembly.FullLocalPath();
            var calamariConfigFilePath = calamariFullPath + ".config";
            if (!File.Exists(calamariConfigFilePath))
                throw new FileNotFoundException($"Unable to find {calamariConfigFilePath} which means the config file would not have been included in testing {calamariFullPath}");

            var calamariAzureFullPath = typeof(AzureWebAppConvention).Assembly.FullLocalPath();
            var calamariAzureConfigFilePath = calamariAzureFullPath + ".config";
            if (!File.Exists(calamariAzureConfigFilePath))
                throw new FileNotFoundException($"Unable to find {calamariAzureConfigFilePath} which means the config file would not have been included in testing {calamariAzureFullPath}");
        }
    }
}