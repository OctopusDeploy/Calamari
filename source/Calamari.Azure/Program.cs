using Calamari.Azure.Integration;
using Calamari.Integration.Scripting;
using Calamari.Plugins;

namespace Calamari.Azure
{
    public class AzureCalamariPlugin : ICalamariPlugin
    {
        public IScriptEngine GetPowershellScriptEngine()
            => new AzurePowerShellScriptEngine();
    }
}