using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Calamari.Deployment;
using Calamari.Integration.Scripting.Bash;
using Calamari.Integration.Scripting.FSharp;
using Calamari.Integration.Scripting.ScriptCS;
using Calamari.Integration.Scripting.WindowsPowerShell;
using Octopus.CoreUtilities.Extensions;
using Octostache;

namespace Calamari.Integration.Scripting
{
    public class ScriptEngineRegistry
    {      
        /// <summary>
        /// The base script engines that can optionally be wrapped up with additional decorators
        /// </summary>
        private readonly Dictionary<ScriptType, IScriptEngine> scriptEngines = new Dictionary<ScriptType, IScriptEngine>
        {
            {ScriptType.Powershell, new PowerShellScriptEngine()},
            {ScriptType.ScriptCS, new ScriptCSScriptEngine()},
            {ScriptType.Bash, new BashScriptEngine()},
            {ScriptType.FSharp, new FSharpEngine()}
        };

        private readonly List<Assembly> assemblies = new List<Assembly> {Assembly.GetExecutingAssembly()};
        public static readonly ScriptEngineRegistry Instance = new ScriptEngineRegistry();

        public void RegisterAssemblies(params Assembly[] assemblies)
        {
            this.assemblies.AddRange(assemblies);
        }

        public IScriptEngine GetScriptEngine(string[] scriptEngineDecorators, ScriptType scriptType)
        {
            return GetDecorators()
                // The decorator needs to match the script type we are working with
                .Where(decorator => decorator.GetSupportedTypes().Contains(scriptType))
                // The decorator needs to be in the supplied list of names
                .Where(decorator => scriptEngineDecorators?.Any(name => name == decorator.Name) ?? false)
                .ToList()
                // Some sanity checking to ensure we found the expected decorators
                .Tee(decorators =>
                {
                    if (decorators.Count != (scriptEngineDecorators?.Length ?? 0))
                        Log.Warn("Some script engine decorators were missing. Expected to find decorators for " + string.Join(",", scriptEngineDecorators) + ".");
                })
                // Link up the decorators.
                .Aggregate(scriptEngines[scriptType], (lastDecorator, currentDecorator) =>
                {
                    currentDecorator.Parent = lastDecorator;
                    return currentDecorator;
                });
        }

        public IScriptEngineDecorator[] GetDecorators() =>
            assemblies
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => typeof(IScriptEngineDecorator).IsAssignableFrom(type) && !type.IsAbstract && !type.IsInterface)
                .Distinct()
                .Select(type => (IScriptEngineDecorator)Activator.CreateInstance(type))
                .ToArray()
                .Tee(decorators => Log.Verbose("Found " + decorators.Length + " script engine decorators"));
    }
}