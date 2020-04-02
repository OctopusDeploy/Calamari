using Autofac;
using Calamari.Commands.Support;
using Calamari.Integration.Proxies;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Autofac.Core;
using Autofac.Core.Registration;
using Calamari.Commands;
using Calamari.Commands.Java;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Deployment.Journal;
using Calamari.HealthChecks;
using Calamari.Hooks;
using Calamari.Integration.Certificates;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Calamari.Integration.Substitutions;
using Calamari.Kubernetes.Commands;
using Calamari.Util.Environments;
using Calamari.Variables;
using Calamari.Plumbing;

namespace Calamari
{
    public class Program
    {
        readonly ILog log;

        protected Program(ILog log)
        {
            this.log = log;
        }
        
        public static int Main(string[] args)
        {
            try
            {
                SecurityProtocols.EnableAllSecurityProtocols();

                var options = CommonOptions.Parse(args);
                return new Program(ConsoleLog.Instance).Run(options);
            }
            catch (Exception ex)
            {
                return ConsoleFormatter.PrintError(ConsoleLog.Instance, ex);
            }
        }

        internal int Run(CommonOptions options)
        {
            log.Verbose($"Calamari Version: {typeof(Program).Assembly.GetInformationalVersion()}");

            if (options.Command.Equals("version", StringComparison.OrdinalIgnoreCase))
                return 0;

            var envInfo = string.Join($"{Environment.NewLine}  ", EnvironmentHelper.SafelyGetEnvironmentInformation());
            log.Verbose($"Environment Information: {Environment.NewLine}  {envInfo}");

            EnvironmentHelper.SetEnvironmentVariable(SpecialVariables.CalamariWorkingDirectory, Environment.CurrentDirectory);
            ProxyInitializer.InitializeDefaultProxy();

            using (var container = BuildContainer(options).Build())
            {
                container.Resolve<VariableLogger>().LogVariables();

                ICommand command;
                try
                {
                    command = container.ResolveNamed<ICommand>(options.Command);
                }
                catch (Exception e) when (e is ComponentNotRegisteredException || e is DependencyResolutionException)
                {
                    throw new CommandException($"Could not find the command {options.Command}");
                }

                return command.Execute(options.RemainingArguments.ToArray());
            }
        }

        protected virtual ContainerBuilder BuildContainer(CommonOptions options)
        {
            var fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
            var builder = new ContainerBuilder();
            builder.RegisterInstance(fileSystem).As<ICalamariFileSystem>();
            builder.RegisterType<VariablesFactory>().AsSelf();
            builder.Register(c => c.Resolve<VariablesFactory>().Create(options)).As<IVariables>().SingleInstance();
            builder.RegisterType<ScriptEngine>().As<IScriptEngine>();
            builder.RegisterType<VariableLogger>().AsSelf();
            builder.RegisterInstance(log).As<ILog>().SingleInstance();
            builder.RegisterType<CalamariCertificateStore>().As<ICertificateStore>().SingleInstance();
            builder.RegisterType<FreeSpaceChecker>().As<IFreeSpaceChecker>().SingleInstance();
            builder.RegisterType<DeploymentJournalWriter>().As<IDeploymentJournalWriter>().SingleInstance();
            builder.RegisterType<CommandLineRunner>().As<ICommandLineRunner>().SingleInstance();
            builder.RegisterType<PackageStore>().As<IPackageStore>().SingleInstance();
            builder.RegisterType<CombinedPackageExtractor>().As<ICombinedPackageExtractor>();
            builder.RegisterType<FileSubstituter>().As<IFileSubstituter>();
            builder.RegisterType<SubstituteInFiles>().As<ISubstituteInFiles>();
            builder.RegisterType<ExtractPackage>().As<IExtractPackage>();

            var assemblies = GetAllAssembliesToRegister(options).ToArray();

            builder.RegisterAssemblyTypes(assemblies)
                .AssignableTo<IScriptWrapper>()
                .Except<TerminalScriptWrapper>()
                .As<IScriptWrapper>()
                .SingleInstance();

            builder.RegisterAssemblyTypes(assemblies)
                .AssignableTo<IDoesDeploymentTargetTypeHealthChecks>()
                .As<IDoesDeploymentTargetTypeHealthChecks>()
                .SingleInstance();

            RegisterExtensions(builder, assemblies);
            
            // known commands are registered last, as last one wins so the extensions can't override the known commands
            RegisterKnownCommands(builder);
            
            return builder;
        }

        static void RegisterExtensions(ContainerBuilder builder, Assembly[] assemblies)
        {
            foreach (var assembly in assemblies)
            {
                var extensionTypes = assembly.GetExportedTypes()
                    .Where(t => typeof(ICalamariExtension).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);
                foreach (var extensionType in extensionTypes)
                {
                    var extensionInstance = (ICalamariExtension) Activator.CreateInstance(extensionType);
                    
                    extensionInstance.Load(builder);

                    // use named registrations for the commands
                    var commands = extensionInstance.RegisterCommands();
                    foreach (var command in commands)
                    {
                        builder.RegisterType(command.Value).Named<ICommand>(command.Key);
                    }
                }
            }
        }

        static void RegisterKnownCommands(ContainerBuilder builder)
        {
            builder.RegisterType(typeof(ApplyDeltaCommand)).Named<ICommand>("apply-delta");
            
            builder.RegisterType(typeof(CleanCommand)).Named<ICommand>("clean");
            builder.RegisterType(typeof(DeployPackageCommand)).Named<ICommand>("deploy-package");
            builder.RegisterType(typeof(DownloadPackageCommand)).Named<ICommand>("download-package");
            builder.RegisterType(typeof(ExtractToStagingCommand)).Named<ICommand>("extract-package-to-staging");
            builder.RegisterType(typeof(FindPackageCommand)).Named<ICommand>("find-package");
            builder.RegisterType(typeof(HealthCheckCommand)).Named<ICommand>("health-check");
            
#if WINDOWS_CERTIFICATE_STORE_SUPPORT
            builder.RegisterType(typeof(ImportCertificateCommand)).Named<ICommand>("import-certificate");
#endif

            builder.RegisterType(typeof(RunScriptCommand)).Named<ICommand>("run-script");

            builder.RegisterType(typeof(DeployJavaArchiveCommand)).Named<ICommand>("deploy-java-archive");
            builder.RegisterType(typeof(JavaLibraryCommand)).Named<ICommand>("java-library");
            
            builder.RegisterType(typeof(TransferPackageCommand)).Named<ICommand>("transfer-package");
            
            builder.RegisterType(typeof(HelmUpgradeCommand)).Named<ICommand>("helm-upgrade");
        }

        static IEnumerable<Assembly> GetAllAssembliesToRegister(CommonOptions options)
        {
            yield return typeof(Program).Assembly; // Calamari
            yield return typeof(ApplyDeltaCommand).Assembly; // Calamari.Shared
            foreach (var extension in options.Extensions)
                yield return Assembly.Load(extension) ?? throw new CommandException($"Could not find the extension {extension}");
        }
    }
}