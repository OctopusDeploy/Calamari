using System.Collections.Generic;

namespace Calamari.Integration.EmbeddedResources
{
    public interface ICalamariEmbeddedResources
    {
        IEnumerable<string> GetEmbeddedResourceNames();
        string GetEmbeddedResourceText(string name);
    }
}