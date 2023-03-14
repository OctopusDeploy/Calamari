using System.IO;
using Calamari.Testing.Helpers;

namespace Calamari.Tests.KubernetesFixtures.ResourceStatus
{
    public static class TestFileLoader
    {
        public static string Load(string filename)
        {
            var filePath = TestEnvironment.GetTestPath("KubernetesFixtures", "ResourceStatus", "assets", filename);
            return File.ReadAllText(filePath);
        }
    }
}