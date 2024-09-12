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
using Calamari.Kubernetes.Helm;
using Calamari.Kubernetes.Integration;
using Calamari.Kubernetes.ResourceStatus;
using Calamari.LaunchTools;
using IContainer = Autofac.IContainer;
using Calamari.Aws.Deployment;
using Calamari.Azure.Kubernetes.Discovery;
using Calamari.FullFrameworkTools.Contracts.Iis;
using Calamari.FullFrameworkTools.Contracts.WindowsCertStore;
#if IIS_SUPPORT
using Calamari.FullFrameworkTools.Iis;
#endif
using Calamari.Integration.Iis;
#if WINDOWS_CERTIFICATE_STORE_SUPPORT
using Calamari.FullFrameworkTools.WindowsCertStore;
#endif
using Calamari.Kubernetes;
using Calamari.Kubernetes.Commands.Executors;

namespace Calamari
{
    public class Program : CalamariFlavourProgram
    {
        protected Program(ILog log) : base(log)
        {
        }

        public static int Main(string[] args)
        {
            return new Program(ConsoleLog.Instance).Execute(args);
        }

        public int Execute(params string[] args)
        {
            return Run(args);
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
            builder.RegisterType<RunningResourceStatusCheck>().As<IRunningResourceStatusCheck>().SingleInstance();
            builder.RegisterType<ResourceStatusCheckTask>().AsSelf();
            builder.RegisterType<ResourceUpdateReporter>().As<IResourceUpdateReporter>().SingleInstance();
            builder.RegisterType<ManifestReporter>().As<IManifestReporter>().SingleInstance();
            builder.RegisterType<ResourceStatusReportExecutor>().As<IResourceStatusReportExecutor>();
            builder.RegisterType<GatherAndApplyRawYamlExecutor>().As<IRawYamlKubernetesApplyExecutor>();
            builder.RegisterType<KustomizeExecutor>().As<IKustomizeKubernetesApplyExecutor>();
            builder.RegisterType<Timer>().As<ITimer>();
            builder.RegisterType<Kubectl>().AsSelf().As<IKubectl>().InstancePerLifetimeScope();
            builder.RegisterType<KubectlGet>().As<IKubectlGet>().SingleInstance();
            builder.RegisterType<HelmTemplateValueSourcesParser>().AsSelf().SingleInstance();


#if WINDOWS_CERTIFICATE_STORE_SUPPORT || IIS_SUPPORT
            builder.RegisterType<FullFrameworkLog>().As<Calamari.FullFrameworkTools.Contracts.ILog>();
#endif
#if WINDOWS_CERTIFICATE_STORE_SUPPORT
            builder.RegisterType<WindowsX509CertificateStore>().As<IWindowsX509CertificateStore>().SingleInstance();
#else
            builder.RegisterType<NoOpWindowsX509CertificateStore>().As<IWindowsX509CertificateStore>().SingleInstance();
#endif
#if IIS_SUPPORT
            builder.RegisterType<InternetInformationServer>().As<IInternetInformationServer>().SingleInstance();
#else
            builder.RegisterType<NoOpInternetInformationServer>().As<IInternetInformationServer>().SingleInstance();
#endif
            
            builder.RegisterType<KubernetesDiscovererFactory>()
                   .As<IKubernetesDiscovererFactory>()
                   .SingleInstance();

            builder.RegisterInstance(new SystemSemaphoreManager()).As<ISemaphoreFactory>();

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
            //Calamari.Aws
            yield return typeof(AwsSpecialVariables).Assembly;
            //Calamari.Azure, this includes AzureOidcAccount
            yield return typeof(AzureKubernetesDiscoverer).Assembly;
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
    
    public class FullFrameworkLog: Calamari.FullFrameworkTools.Contracts.ILog
    {
        readonly ILog log;

        public FullFrameworkLog(ILog log)
        {
            this.log = log;
        }
        public void Verbose(string message)
        {
            log.Verbose(message);
        }

        public void Info(string message)
        {
            log.Info(message);
        }

        public void ErrorFormat(string messageFormat, params object[] args)
        {
            log.ErrorFormat(messageFormat, args);
        }
    }
}