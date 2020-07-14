using System.Collections.Generic;
using System.Reflection;

namespace Calamari.CommonTemp
{
    public interface ICalamariEmbeddedResources
    {
        IEnumerable<string> GetEmbeddedResourceNames(Assembly assembly);
        string GetEmbeddedResourceText(Assembly assembly, string name);
    }
}