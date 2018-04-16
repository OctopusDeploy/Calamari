using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Calamari.Commands.Support;

namespace Calamari.Plugins
{
    public static class PluginLoader
    {
        public static (ICalamariPlugin[] plugins, string[] remainingOptions) Load(string[] commandLineArgs)
        {
            var (paths, remainingOptions) = GetPluginPaths(commandLineArgs);
            var plugins = LoadAssemblies(paths)
                .SelectMany(a => a.GetTypes())
                .Where(t => typeof(ICalamariPlugin).IsAssignableFrom(t))
                .Select(t => (ICalamariPlugin) Activator.CreateInstance(t))
                .ToArray();
            
            return (plugins, remainingOptions);
        }

        private static List<Assembly> LoadAssemblies(string[] paths)
        {
            var load = new List<Assembly>();
            foreach (var path in paths)
            {
                try
                {
                    load.Add(Assembly.LoadFrom(path));
                }
                catch (Exception ex)
                {
                    throw new Exception($"Exception loading plugin {path}", ex);
                }
            }
            return load;
        }

        private static (string[] paths, string[] remainingOptions) GetPluginPaths(string[] commandLineArgs)
        {
            var paths = new List<string>();
            var options = new OptionSet()
                {{"plugin=", "The filename of a DLL to load as a plugin", v => paths.Add(v)}};

            var remainingOptions = options.Parse(commandLineArgs);
            return (paths.ToArray(), remainingOptions.ToArray());
        }
    }
}