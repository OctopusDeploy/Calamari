using System.Linq;

namespace Calamari.Util
{
    public class PluginUtils : IPluginUtils
    {
        public string GetFirstArgument(string[] args)
        {
            return Sanitise(args?.FirstOrDefault() ?? string.Empty);
        }

        public string GetSecondArgument(string[] args)
        {
            return Sanitise(args?.Skip(1).FirstOrDefault() ?? string.Empty);
        }

        private string Sanitise(string input) => input.Trim('-', '/');
    }
}
