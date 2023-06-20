using Newtonsoft.Json.Linq;

namespace Calamari.Tests.KubernetesFixtures
{
    public static class JObjectExtensionMethods
    {
        public static T Get<T>(this JObject jsonOutput, params string[] paths)
        {
            JToken result = jsonOutput;
            
            foreach (var path in paths)
            {
                result = result[path];
                if (result is null)
                {
                    return default;
                }
            }

            return result.Value<T>();
        }
    }
}