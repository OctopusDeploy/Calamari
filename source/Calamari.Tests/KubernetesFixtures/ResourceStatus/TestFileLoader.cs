using System.IO;

namespace Calamari.Tests.KubernetesFixtures.ResourceStatus
{
    public static class TestFileLoader
    {
        public static string Load(string filename)
        {
            return File.ReadAllText(Path.Combine("KubernetesFixtures", "ResourceStatus", "assets", filename));
        }
    }
}