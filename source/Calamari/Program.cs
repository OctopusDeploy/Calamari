using Autofac;
using Calamari.Commands.Support;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Calamari.Commands;
using Calamari.Common;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.Deployment.Journal;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Proxies;
using Calamari.Integration.Certificates;
using Calamari.Integration.FileSystem;
using NuGet;

namespace Calamari
{
    public class Program : CalamariFlavourProgram
    {
        List<string> extensions;

        protected Program(ILog log) : base(log)
        {
        }

        public static int Main(string[] args)
        {
            ProxyInitializer.InitializeDefaultProxy();
            return new Program(ConsoleLog.Instance).Run(args);
        }

        protected override int ResolveAndExecuteCommand(IContainer container, CommonOptions options)
        {
            var command = container.Resolve<ICommandWithArgs[]>();
            if (command.Length == 0)
                throw new CommandException($"Could not find the command {options.Command}");
            if (command.Length > 1)
                throw new CommandException($"Multiple commands found with the name {options.Command}");

            return command[0].Execute(options.RemainingArguments.ToArray());
        }

        protected override void ConfigureContainer(ContainerBuilder builder, CommonOptions options)
        {
            // Setting extensions here as in the new Modularity world we don't register extensions
            // and GetAllAssembliesToRegister doesn't get passed CommonOptions
            extensions = options.Extensions;

            base.ConfigureContainer(builder, options);

            builder.RegisterType<CalamariCertificateStore>().As<ICertificateStore>().SingleInstance();
            builder.RegisterType<DeploymentJournalWriter>().As<IDeploymentJournalWriter>().SingleInstance();
            builder.RegisterType<PackageStore>().As<IPackageStore>().SingleInstance();

            builder.RegisterAssemblyTypes(GetAllAssembliesToRegister().ToArray())
                .AssignableTo<ICommandWithArgs>()
                .Where(t => t.GetCustomAttribute<CommandAttribute>().Name
                    .Equals(options.Command, StringComparison.OrdinalIgnoreCase))
                .As<ICommandWithArgs>();
        }

        IEnumerable<Assembly> GetExtensionAssemblies()
        {
            foreach (var extension in extensions)
                yield return Assembly.Load(extension) ?? throw new CommandException($"Could not find the extension {extension}");
        }

        protected override IEnumerable<Assembly> GetAllAssembliesToRegister()
        {
            var assemblies = base.GetAllAssembliesToRegister();
            foreach (var assembly in assemblies)
            {
                yield return assembly;
            }

            yield return typeof(ApplyDeltaCommand).Assembly; // Calamari.Shared

            var extensionAssemblies = GetExtensionAssemblies();
            foreach (var extensionAssembly in extensionAssemblies)
            {
                yield return extensionAssembly;
            }
        }
    }
}