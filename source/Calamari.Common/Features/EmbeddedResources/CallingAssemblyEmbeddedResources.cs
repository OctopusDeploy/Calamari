using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Calamari.Common.Features.EmbeddedResources
{
    public class AssemblyEmbeddedResources : ICalamariEmbeddedResources
    {
        public IEnumerable<string> GetEmbeddedResourceNames(Assembly assembly)
        {
            return assembly.GetManifestResourceNames();
        }

        public string GetEmbeddedResourceText(Assembly assembly, string name)
        {
            using (var stream = assembly.GetManifestResourceStream(name))
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }
    }
}