using System.Collections.Generic;
using System.Reflection;

namespace Calamari.Common.Features.EmbeddedResources
{
    public interface ICalamariEmbeddedResources
    {
        IEnumerable<string> GetEmbeddedResourceNames(Assembly assembly);
        string GetEmbeddedResourceText(Assembly assembly, string name);
    }
}