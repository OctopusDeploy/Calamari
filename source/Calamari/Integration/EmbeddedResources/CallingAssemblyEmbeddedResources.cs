using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Calamari.Integration.EmbeddedResources
{
    public class CallingAssemblyEmbeddedResources : ICalamariEmbeddedResources
    {
        public IEnumerable<string> GetEmbeddedResourceNames()
        {
            return Assembly.GetCallingAssembly().GetManifestResourceNames();
        }

        public string GetEmbeddedResourceText(string name)
        {
            using (var stream = Assembly.GetCallingAssembly().GetManifestResourceStream(name))
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }
    }
}