using System.IO;
using Calamari.Commands;
using Calamari.Integration.Processes;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures
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
        }
    }
}