using Autofac;
using Calamari.Commands.Support;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Autofac.Features.Metadata;
using Calamari.Commands;
using Calamari.Common;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes.Semaphores;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.Deployment.Journal;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.Deployment.PackageRetention.VersionFormatDiscovery;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.PackageRetention;
using Calamari.Deployment.PackageRetention.Caching;
using Calamari.Deployment.PackageRetention.Model;
using Calamari.Deployment.PackageRetention.Repositories;
using Calamari.Integration.Certificates;
using Calamari.Integration.FileSystem;
using Calamari.LaunchTools;
using Octopus.Versioning;
using IContainer = Autofac.IContainer;
using VersionConverter = Newtonsoft.Json.Converters.VersionConverter;

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
            return new Program(ConsoleLog.Instance).Run(args);
        }

        protected override int ResolveAndExecuteCommand(IContainer container, CommonOptions options)
        {
            var lockingCommands = container.ResolveKeyed<IEnumerable<Meta<Lazy<ICommandWithArgs>, CommandMeta>>>(nameof(PackageLockingCommandAttribute));
            var commands = container.Resolve<IEnumerable<Meta<Lazy<ICommandWithArgs>, CommandMeta>>>()
                                    .Where(c => lockingCommands.All(lc => !lc.Metadata.Name.Equals(c.Metadata.Name, StringComparison.OrdinalIgnoreCase)))
                                    .Union(lockingCommands);

            var commandCandidates = commands.Where(x => x.Metadata.Name.Equals(options.Command, StringComparison.OrdinalIgnoreCase)).ToArray();

            if (commandCandidates.Length == 0)
                throw new CommandException($"Could not find the command {options.Command}");
            if (commandCandidates.Length > 1)
                throw new CommandException($"Multiple commands found with the name {options.Command}");

            return commandCandidates[0].Value.Value.Execute(options.RemainingArguments.ToArray());
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

            builder.RegisterInstance(SemaphoreFactory.Get()).As<ISemaphoreFactory>();
            builder.RegisterType<JsonJournalRepositoryFactory>().As<IJournalRepositoryFactory>();
            builder.RegisterType<Journal>().As<IManagePackageUse>();
            builder.RegisterType<LeastFrequentlyUsedWithAgingCacheAlgorithm>().As<IRetentionAlgorithm>();

            //Note: this could be done with a factory, but it just felt like overkill for this.
            var versionFormatDiscovery = GetProgramAssemblyToRegister()
                                         .GetTypes()
                                         .Where(p => typeof(ITryToDiscoverVersionFormat).IsAssignableFrom(p));

            var discoveryInstances = versionFormatDiscovery.Select(d => Activator.CreateInstance(d) as ITryToDiscoverVersionFormat).ToArray();
            PackageIdentity.SetVersionFormatDiscoverers(discoveryInstances);

            //Add decorator to commands with the RetentionLockingCommand attribute. Also need to include commands defined in external assemblies.
            var assembliesToRegister = GetAllAssembliesToRegister().ToArray();

            //TODO: Do this using Autofac
            TypeDescriptor.AddAttributes(typeof(ServerTaskId), new TypeConverterAttribute(typeof(TinyTypeTypeConverter<ServerTaskId>)));
            
            var typesToAlwaysDecorate = new Type[] { typeof(ApplyDeltaCommand) }; //Commands from external assemblies.

            //Get register commands with the RetentionLockingCommand attribute;
            builder.RegisterAssemblyTypes(assembliesToRegister)
                   .Where(t => t.HasAttribute<PackageLockingCommandAttribute>()
                               || typesToAlwaysDecorate.Contains(t))
                   .AssignableTo<ICommandWithArgs>()
                   .WithMetadataFrom<CommandAttribute>()
                   .Named<ICommandWithArgs>(nameof(PackageLockingCommandAttribute) + "From");

            //Register the decorator for the above commands.  Uses the old Autofac method because we're only on v4.8
            builder.RegisterDecorator<ICommandWithArgs>((c, inner)
                                                            => new PackageJournalCommandDecorator(c.Resolve<ILog>(),
                                                                                           inner,
                                                                                           c.Resolve<IManagePackageUse>()),
                                                        fromKey: nameof(PackageLockingCommandAttribute) + "From",
                                                        toKey: nameof(PackageLockingCommandAttribute));

            //Register the non-decorated commands
            builder.RegisterAssemblyTypes(assembliesToRegister)
                   .Where(c => !c.HasAttribute<PackageLockingCommandAttribute>() && c != typeof(PackageJournalCommandDecorator))
                   .AssignableTo<ICommandWithArgs>()
                   .WithMetadataFrom<CommandAttribute>()
                   .As<ICommandWithArgs>();

            builder.RegisterAssemblyTypes(assembliesToRegister)
                   .AssignableTo<ICommandWithInputs>()
                   .WithMetadataFrom<CommandAttribute>()
                   .As<ICommandWithInputs>();

            builder.RegisterAssemblyTypes(GetProgramAssemblyToRegister())
                   .Where(x => typeof(ILaunchTool).IsAssignableFrom(x) && !x.IsAbstract && !x.IsInterface)
                   .WithMetadataFrom<LaunchToolAttribute>()
                   .As<ILaunchTool>();
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

    static class ExtensionMethods
    {
        public static bool HasAttribute<T>(this Type type) where T : Attribute
        {
            return type.GetCustomAttributes(false).Any(a => a is T);
        }
    }
}