using Calamari.Commands.Support;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Calamari.Modules;
using Calamari.Util;
using Octodiff;
using Module = Autofac.Module;

namespace Calamari.Extensions
{
    /// <summary>
    /// This class provides a way to find all the modules in a given assembly
    /// </summary>
    public class ModuleLoader
    {
        private static readonly IPluginUtils PluginUtils = new PluginUtils();
        private readonly OptionSet optionSet = new OptionSet();
        private readonly string firstCommand;
        private IList<string> extensions;

        public IEnumerable<Module> AllModules => Modules.Union(CommandModules);

        public IEnumerable<Module> Modules =>
            extensions?
                .Select(extension => GetAssemblyByName("Calamari." + extension))
                .Where(assembly => assembly != null)
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => typeof(Module).IsAssignableFrom(type))
                .Where(type => !type.IsAbstract)
                .Where(type => !type.IsInterface)
                .Select(Activator.CreateInstance)
                .Select(module => (Module) module)
            ?? Enumerable.Empty<Module>();

        public IEnumerable<Module> CommandModules =>
            extensions?
                    .Select(extension => GetAssemblyByName("Calamari." + extension))
                    .Where(assembly => assembly != null)
                    .Select(assembly => new CalamariCommandsModule(firstCommand, assembly))
            ?? Enumerable.Empty<Module>();        

        public ModuleLoader(string[] args)
        {
            optionSet.Add("extensions=", "List of Calamari extensions to load.", v => extensions = ProcessExtensions(v));
            optionSet.Parse(args);
            firstCommand = PluginUtils.GetFirstArgument(args);
        }

        private IList<string> ProcessExtensions(string rawExtensions) =>
            rawExtensions?
                .Split(',')
                .Select(ext => ext.Trim())
                .Where(ext => !string.IsNullOrWhiteSpace(ext))
                .ToList();

        private Assembly GetAssemblyByName(string name) =>
            AppDomain
                .CurrentDomain.GetAssemblies()
                .SingleOrDefault(assembly => assembly.GetName().Name == name);       
    }
}
