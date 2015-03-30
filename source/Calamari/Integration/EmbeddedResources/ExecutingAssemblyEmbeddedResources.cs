using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Calamari.Integration.EmbeddedResources
{
    public class ExecutingAssemblyEmbeddedResources : ICalamariEmbeddedResources
    {
        public IEnumerable<string> GetEmbeddedResourceNames()
        {
            return Assembly.GetExecutingAssembly().GetManifestResourceNames();
        }

        public string GetEmbeddedResourceText(string name)
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }
    }
}