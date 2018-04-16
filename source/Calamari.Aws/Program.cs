using Calamari.Aws.Integration;
using Calamari.Integration.Scripting;
using Calamari.Plugins;

namespace Calamari.Aws
{
    public class AwsCalamariPlugin : ICalamariPlugin
    {
        public IScriptEngine GetPowershellScriptEngine()
            => new AwsPowerShellScriptEngine();
    }
}