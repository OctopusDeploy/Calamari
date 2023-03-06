using System.IO;

namespace Calamari.Tests.KubernetesFixtures.ResourceStatus
{
    public static class ResourceLoader
    {
        public static string Load(string filename)
        {
            return File.ReadAllText(Path.Combine("KubernetesFixtures", "ResourceStatus", "Resources", filename));
        }
    }
}