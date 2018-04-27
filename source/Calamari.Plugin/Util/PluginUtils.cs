using System.Linq;

namespace Calamari.Util
{
    public class PluginUtils : IPluginUtils
    {
        public string GetFirstArgument(string[] args)
        {
            return (args?.FirstOrDefault() ?? string.Empty).Trim('-', '/');
        }
    }
}
