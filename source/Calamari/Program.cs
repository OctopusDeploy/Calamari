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
using Calamari.Common.Features.Discovery;
using Calamari.Common.Features.Processes.Semaphores;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.Deployment.Journal;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.Logging;
using Calamari.Deployment.PackageRetention;
using Calamari.Integration.Certificates;
using Calamari.Integration.FileSystem;
using Calamari.Kubernetes.Commands.Discovery;
using Calamari.Kubernetes.ResourceStatus;
using Calamari.LaunchTools;
using IContainer = Autofac.IContainer;
#if !NET40
using Calamari.Aws.Deployment;
using Calamari.Azure;
#endif

namespace Calamari
{
    public class Program : CalamariFlavourProgram
    {
        protected Program(ILog log) : base(log)
        {
        }

        public static int Main(string[] args)
        {
            return new Program(ConsoleLog.Instance).Run(args);
        }

        protected override int ResolveAndExecuteCommand(IContainer container, CommonOptions options)
        {
            var commands = container.Resolve<IEnumerable<Meta<Lazy<ICommandWithArgs>, CommandMeta>>>();

            var commandCandidates = commands.Where(x => x.Metadata.Name.Equals(options.Command, StringComparison.OrdinalIgnoreCase)).ToArray();

            if (commandCandidates.Length == 0)
                throw new CommandException($"Could not find the command {options.Command}");
            if (commandCandidates.Length > 1)
                throw new CommandException($"Multiple commands found with the name {options.Command}");

            return commandCandidates[0].Value.Value.Execute(options.RemainingArguments.ToArray());
        }

        protected override void ConfigureContainer(ContainerBuilder builder, CommonOptions options)
        {
            base.ConfigureContainer(builder, options);

            builder.RegisterType<CalamariCertificateStore>().As<ICertificateStore>().SingleInstance();
            builder.RegisterType<DeploymentJournalWriter>().As<IDeploymentJournalWriter>().SingleInstance();
            builder.RegisterType<PackageStore>().As<IPackageStore>().SingleInstance();
            builder.RegisterType<ResourceRetriever>().As<IResourceRetriever>().SingleInstance();
            builder.RegisterType<ResourceStatusChecker>().As<IResourceStatusChecker>().SingleInstance();
            builder.RegisterType<ResourceUpdateReporter>().As<IResourceUpdateReporter>().SingleInstance();
            builder.RegisterType<ResourceStatusReportExecutor>().AsSelf();
            builder.RegisterType<KubectlGet>().As<IKubectlGet>().SingleInstance();

            builder.RegisterType<KubernetesDiscovererFactory>()
                   .As<IKubernetesDiscovererFactory>()
                   .SingleInstance();

            builder.RegisterInstance(SemaphoreFactory.Get()).As<ISemaphoreFactory>();

            builder.RegisterModule<PackageRetentionModule>();

            TypeDescriptor.AddAttributes(typeof(ServerTaskId), new TypeConverterAttribute(typeof(TinyTypeTypeConverter<ServerTaskId>)));

            //Add decorator to commands with the RetentionLockingCommand attribute. Also need to include commands defined in external assemblies.
            var assembliesToRegister = GetAllAssembliesToRegister().ToArray();

            builder.RegisterAssemblyModules(assembliesToRegister);

            builder.RegisterAssemblyTypes(assembliesToRegister)
                   .AssignableTo<IKubernetesDiscoverer>()
                   .As<IKubernetesDiscoverer>();

            //Register the non-decorated commands
            builder.RegisterAssemblyTypes(assembliesToRegister)
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
#if !NET40
            //Calamari.Aws
            yield return typeof(AwsSpecialVariables).Assembly;
            //Calamari.Azure
            yield return typeof(ServicePrincipalAccount).Assembly;
#else
                return Enumerable.Empty<Assembly>();
#endif
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