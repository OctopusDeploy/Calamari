using Calamari.Commands.Support;
using Calamari.Modules;
using Calamari.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Module = Autofac.Module;

namespace Calamari.Extensions
{

    public class ModuleLoaderNew
    {
        private readonly OptionSet optionSet = new OptionSet();
        private List<string> extensions = new List<string>();
        
        ModuleLoaderNew(string[] args)
        {
            optionSet.Add("extensions=", "List of Calamari extensions to load.", ExtractNamedExtensions);
            optionSet.Parse(args);
        }

        private void ExtractNamedExtensions(string v)
        {
            extensions.AddRange(v?.Split(',').Select(ext => ext.Trim()).Where(ext => !string.IsNullOrWhiteSpace(ext)) ?? new string[0]);
        }

        public static string[] GetExtensions(string[] args)
        {
            return new ModuleLoaderNew(args).extensions.ToArray();
        }
    }


    /// <summary>
    /// This class provides a way to find all the modules in a given assembly.
    /// Using this module will take care of registering a ICommand to be run, as
    /// well as a ICommand to be supplied to the help command (if the help command
    /// was requested).
    ///
    /// And Autofac modules in the supplied assembly will also be loaded.
    ///
    /// What this means is that any ICommand classes in an extension module will be loaded
    /// automatically, *without* those classes being added to a custom module class.
    /// Extension modules that only contribute ICommand classes don't need to provide
    /// any additional Auotfac modules.
    ///
    /// Any other kind of services (hooks like IScriptWrapper especially) do need to be
    /// added to a module class.
    /// </summary>
    public class ModuleLoader
    {
        private static readonly IPluginUtils PluginUtils = new PluginUtils();
        private readonly OptionSet optionSet = new OptionSet();
        private readonly string firstCommand;
        private readonly string secondCommand;
        private IList<string> extensions;

        public IEnumerable<Module> AllModules => Modules.Union(CommandModules);

        IEnumerable<Module> Modules =>
            extensions?
                .Select(GetAssemblyByName)
                .Where(assembly => assembly != null)
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => typeof(Module).IsAssignableFrom(type))
                .Where(type => !type.IsAbstract)
                .Where(type => !type.IsInterface)
                .Select(Activator.CreateInstance)
                .Select(module => (Module) module)
            ?? Enumerable.Empty<Module>();

        IEnumerable<Module> CommandModules =>
            extensions?
                    .Select(GetAssemblyByName)
                    .Where(assembly => assembly != null)
                    .Select(assembly => new CalamariCommandsModule(firstCommand, secondCommand, assembly))
            ?? Enumerable.Empty<Module>();

        public ModuleLoader(string[] args)
        {
            optionSet.Add("extensions=", "List of Calamari extensions to load.", v => extensions = ProcessExtensions(v));
            optionSet.Parse(args);
            firstCommand = PluginUtils.GetFirstArgument(args);
            secondCommand = PluginUtils.GetSecondArgument(args);
        }

        private IList<string> ProcessExtensions(string rawExtensions) =>
            rawExtensions?
                .Split(',')
                .Select(ext => ext.Trim())
                .Where(ext => !string.IsNullOrWhiteSpace(ext))
                .ToList();

        private Assembly GetAssemblyByName(string name)
        {
            Assembly.Load(name);
            return AppDomain
                .CurrentDomain.GetAssemblies()
                .SingleOrDefault(assembly => assembly.GetName().Name == name);
        }
    }
}
