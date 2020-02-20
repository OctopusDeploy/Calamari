using Autofac;
using Calamari.Commands.Support;
using Calamari.Integration.Proxies;
using Calamari.Modules;
using Calamari.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reflection;
using Calamari.Commands;
using Calamari.Deployment;
using Calamari.Extensions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Plumbing;
using Calamari.Util.Environments;
using Calamari.Variables;
using NuGet;
using Octostache;

namespace Calamari
{
    public class Program
    {
        public static int Main(string[] args)
        {
            try
            {
                SecurityProtocols.EnableAllSecurityProtocols();

                var options = CommonOptions.Parse(args);

                Log.Verbose($"Calamari Version: {typeof(Program).Assembly.GetInformationalVersion()}");

                if (options.Command.Equals("version", StringComparison.OrdinalIgnoreCase))
                    return 0;

                var envInfo = string.Join($"{Environment.NewLine}  ", EnvironmentHelper.SafelyGetEnvironmentInformation());
                Log.Verbose($"Environment Information: {Environment.NewLine}  {envInfo}");

                var fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
                var variables = VariablesFactory.Create(fileSystem, options);

                var builder = new ContainerBuilder();
                builder.RegisterInstance(fileSystem).As<ICalamariFileSystem>();
                builder.RegisterInstance(variables).As<IVariables>();

                var allAssemblies = GetAllAssembliesToRegister(options).ToArray();

                foreach (var assembly in allAssemblies)
                    builder.RegisterAssemblyModules(assembly);

                builder.RegisterType(FindCommandType(allAssemblies, options)).As<ICommand>();

                using (var container = builder.Build())
                    return container.Resolve<ICommand>().Execute(options.RemainingArguments.ToArray());
            }
            catch (Exception ex)
            {
                return ConsoleFormatter.PrintError(ex);
            }
        }

        static IEnumerable<Assembly> GetAllAssembliesToRegister(CommonOptions options)
        {
            yield return typeof(Program).Assembly; // Calamari
            yield return typeof(ApplyDeltaCommand).Assembly; // Calamari.Shared
            foreach (var extension in options.Extensions)
                yield return Assembly.Load(extension) ?? throw new CommandException($"Could not find the extension {extension}");
        }

        static Type FindCommandType(Assembly[] allAssemblies, CommonOptions options)
        {
            var commandType = allAssemblies
                .SelectMany(a => a.GetExportedTypes())
                .Where(t => !t.IsAbstract && !t.IsInterface)
                .Where(t => t.IsAssignableTo<ICommand>())
                .FirstOrDefault(t => t.GetCustomAttribute<CommandAttribute>().Name.Equals(options.Command, StringComparison.OrdinalIgnoreCase));

            if (commandType == null)
                throw new CommandException($"Could not find the command {options.Command}");
            return commandType;
        }
    }
}