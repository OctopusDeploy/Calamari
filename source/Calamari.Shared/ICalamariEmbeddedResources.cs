using System.Collections.Generic;
using System.Reflection;

namespace Calamari.Shared
{
    public interface ICalamariEmbeddedResources
    {
        IEnumerable<string> GetEmbeddedResourceNames(Assembly assembly);
        string GetEmbeddedResourceText(Assembly assembly, string name);
    }
}