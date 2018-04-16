using System.Collections.Generic;
using Calamari.Integration.Scripting;

namespace Calamari.Plugins
{
    public interface ICalamariPlugin
    {
        IScriptEngine GetPowershellScriptEngine();
    }
}