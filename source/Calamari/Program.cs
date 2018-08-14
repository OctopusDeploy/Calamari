using Autofac;
using Calamari.Commands.Support;
using Calamari.Integration.Proxies;
using Calamari.Modules;
using Calamari.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using Autofac.Features.ResolveAnything;
using Calamari.Commands;
using Calamari.Deployment.Journal;
using Calamari.Extensions;
using Calamari.Integration.ConfigurationTransforms;
using Calamari.Integration.ConfigurationVariables;
using Calamari.Integration.EmbeddedResources;
using Calamari.Integration.FileSystem;
using Calamari.Integration.JsonVariables;
using Calamari.Integration.Packages;
using Calamari.Integration.Packages.NuGet;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Calamari.Integration.ServiceMessages;
using Calamari.Integration.Substitutions;
using Calamari.Shared.Commands;
using Calamari.Shared.FileSystem;
using Calamari.Util.Environments;
using Module = Autofac.Module;
using Calamari.Shared;
using Calamari.Shared.Scripting;
using Octostache;
using IConvention = Calamari.Shared.Commands.IConvention;

namespace Calamari
{

    class CalamariProgramModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            

            builder.Register(context =>
            {
                var dictionary = context.Resolve<VariableDictionary>();
                return new CommandLineRunner(new SplitCommandOutput(new ConsoleCommandOutput(),
                    new ServiceMessageCommandOutput(dictionary)));
            }).As<ICommandLineRunner>().SingleInstance();


            //TODO: This is just here to deal with java exctrator
            builder.Register(context =>
            {
                var dictionary = context.Resolve<VariableDictionary>();
                return new SplitCommandOutput(new ConsoleCommandOutput(),
                    new ServiceMessageCommandOutput(dictionary));
            }).As<ICommandOutput>().SingleInstance();
            
            
            builder.RegisterInstance(new ScriptEngineRegistry()).As<IScriptEngineRegistry>();
            builder.RegisterInstance<ICalamariFileSystem>(CalamariPhysicalFileSystem.GetPhysicalFileSystem());
            builder.RegisterInstance(new LogWrapper()).As<ILog>().SingleInstance();
            
            
                
            builder.RegisterType<AssemblyEmbeddedResources>().As<ICalamariEmbeddedResources>().SingleInstance();
            builder.RegisterType<ConfigurationTransformer>().As<IConfigurationTransformer>().SingleInstance();
            builder.RegisterType<FileSubstituter>().As<IFileSubstituter>().SingleInstance();
            builder.RegisterType<CombinedScriptEngine>().As<IScriptRunner>().SingleInstance();
            //builder.RegisterType<NupkgExtractor>().As<IPackageExtractor>().SingleInstance();
            builder.RegisterType<GenericPackageExtractor>()
                .As<IGenericPackageExtractor>()
                .As<IPackageExtractor>()
                .SingleInstance();
            builder.RegisterType<TransformFileLocator>().As<ITransformFileLocator>().SingleInstance();
            builder.RegisterType<ConfigurationVariablesReplacer>().As<IConfigurationVariablesReplacer>();
            builder.RegisterType<JsonConfigurationVariableReplacer>().As<IJsonConfigurationVariableReplacer>();
            
/*  scriptWrapperHooks,
            VariableDictionary variables,
            ICommandLineRunner commandLineRunner,
            StringDictionary environmentVars = null*/

        }
    }


    public class OptionsBuilder : IOptionsBuilder
    {
        private OptionSet _os;
        public OptionsBuilder(OptionSet os)
        {
            _os = os;
        }
   
        public IOptionsBuilder Add(string prototype, string description, Action<string> action)
        {
            _os.Add(prototype, description, action);
            return this;
        }

        public IOptionsBuilder Add(string prototype, Action<string> action)
        {
            _os.Add(prototype, action);
            return this;
        }
    }
    
    public class Program
    {
        private static readonly IPluginUtils PluginUtils = new PluginUtils();
        /// <summary>
        /// Common assemblies that will always be present
        /// </summary>
        private static readonly Assembly[] KnownAssemblies = new[]
        {
            typeof(Program).Assembly,
            typeof(CalamariCommandsModule).Assembly
        };
        private readonly ICommand command = null;

        public static int Main(string[] args)
        {
            Log.Verbose($"Octopus Deploy: Calamari version {typeof(Program).Assembly.GetInformationalVersion()}");
            Log.Verbose($"Environment Information:{Environment.NewLine}" +
                        $"  {string.Join($"{Environment.NewLine}  ", EnvironmentHelper.SafelyGetEnvironmentInformation())}");
            
            EnableAllSecurityProtocols();
            var firstArg = PluginUtils.GetFirstArgument(args);
            
            
            var commands =
                (from type in typeof(Program).Assembly.GetTypes()
                    where typeof(ICommand).IsAssignableFrom(type)
                    let attribute = (CommandAttribute)type.GetCustomAttributes(typeof(CommandAttribute), true).FirstOrDefault()
                    where attribute != null
                    select new {attribute, type}).ToArray();

            var cmd = commands.FirstOrDefault(t => t.attribute.Name.Equals(firstArg));

            if (cmd != null && !typeof(IDeploymentAction).IsAssignableFrom(cmd.type))
            {
                //This is not a CustomCommand, this is one of the other types used by calamari. e.g. apply-delta, find-package, clean etc.
                var cc = (ICommand) Activator.CreateInstance(cmd.type);
                return cc.Execute(args);
            }
            
            var xe = (from type in typeof(Program).Assembly.GetTypes()
            where typeof(IDeploymentAction).IsAssignableFrom(type)
                let attribute = (DeploymentActionAttribute)type.GetCustomAttributes(typeof(DeploymentActionAttribute), true).FirstOrDefault()
                where attribute != null
                select new {type, attribute}).ToArray();
            
            var deploymentAction = xe.FirstOrDefault(t => t.attribute.Name.Equals(firstArg));
            if (deploymentAction == null)
            {
                throw new CommandException($"Unable to find comnd with name {firstArg}");
            }
            
            var ml = ModuleLoaderNew.GetExtensions(args); //Add
            

            var Options = new OptionSet();
            string variablesFile =null;
                string packageFile=null;
            string base64Variables = null;
            string sensitiveVariablesPassword =null;
            string sensitiveVariablesFile = null;
            Options.Add("variables=", "Path to a JSON file containing variables.", v => variablesFile = Path.GetFullPath(v));
            Options.Add("package=", "Path to the deployment package to install.", v => packageFile = Path.GetFullPath(v));
            Options.Add("sensitiveVariables=", "Password protected JSON file containing sensitive-variables.", v => sensitiveVariablesFile = v);
            Options.Add("sensitiveVariablesPassword=", "Password used to decrypt sensitive-variables.", v => sensitiveVariablesPassword = v);
            Options.Add("base64Variables=", "JSON string containing variables.", v => base64Variables = v);

            //TODO: This exists for DeployJava command... lets get it to use package like everything else
            Options.Add("archive=", "Path to the Java archive to deploy.", v => packageFile = Path.GetFullPath(v));
            Options.Parse(args);

            var variables = new CalamariVariableDictionary(variablesFile, sensitiveVariablesFile, sensitiveVariablesPassword, base64Variables);
            var builder = new ContainerBuilder();
            builder.RegisterSource(new AnyConcreteTypeNotAlreadyRegisteredSource());
            builder.RegisterInstance(variables).As<VariableDictionary>().As<CalamariVariableDictionary>();
            builder.RegisterModule<CalamariProgramModule>();
            builder.RegisterType(deploymentAction.type).AsSelf();
            
            //Embeded Conventions -> Also include module Conventions
            foreach (var type1 in typeof(Program).Assembly.GetTypes().Where(type => typeof(IConvention).IsAssignableFrom(type)))
            {
                builder.RegisterType(type1).AsSelf();
            }
            
            
//            foreach (var type1 in typeof(Program).Assembly.GetTypes().Where(type => typeof(IPackageExtractor).IsAssignableFrom(type)))
//            {
//                builder.RegisterType(type1).As<IPackageExtractor>();
//            }
            
            
            //Embeded Wrappers -> Also include module Wrappers
            foreach (var type1 in typeof(Program).Assembly.GetTypes().Where(type => typeof(IScriptWrapper).IsAssignableFrom(type)))
            {
                builder.RegisterType(type1).As<IScriptWrapper>();
            }
            
            
            var container = builder.Build();
            var cb = new DeploymentStrategyBuilder(container)
            {
                Variables = variables
            };
            ((IDeploymentAction)container.Resolve(deploymentAction.type)).Build(cb);

            //TODO: This should dissapear once custom params are removed
            if (cb.PreExecution != null)
            {
                var Options2 = new OptionSet();
                cb.PreExecution(new OptionsBuilder(Options2), variables);
                Options2.Parse(args);
            }
           
            var cr = new CommandRunner(cb, container.Resolve<ICalamariFileSystem>(), new DeploymentJournalWriter(container.Resolve<ICalamariFileSystem>()));
            cr.Run(variables, packageFile);

            return variables.GetInt32(SpecialVariables.Action.Script.ExitCode) ?? 0;
//            using (var container = BuildContainer(args))
//            {
//                return container.Resolve<Program>().Execute(args);
//            }
        }

        public static List<string> Extensions = new List<string>();

        public static IContainer BuildContainer(string[] args)
        {
            var firstArg = PluginUtils.GetFirstArgument(args);
            var secondArg = PluginUtils.GetSecondArgument(args);

            var builder = new ContainerBuilder();

            // Register the program entry point
            builder.RegisterModule(new CalamariProgramModule());
            // This will register common utilities and services
//            builder.RegisterModule(new CommonModule(args));
            // For all the common locations (i.e. this assembly and the shared one)
            // load any commands, and any commands to support the help command (if
            // required).
            foreach (var knownAssembly in KnownAssemblies)
            {
                builder.RegisterModule(new CalamariCommandsModule(firstArg, secondArg, knownAssembly));
            }
            // For the external libraries, let them load any additional
            // services via their module classes.
            foreach (var module in new ModuleLoader(args).AllModules)
            {
                builder.RegisterModule(module);
            }

            return builder.Build();
        }

//        public Program(ICommand command, HelpCommand helpCommand)
//        {
//            this.command = command;
//            this.helpCommand = helpCommand;
//        }

        public int Execute(string[] args)
        {
            Log.Verbose($"Octopus Deploy: Calamari version {typeof(Program).Assembly.GetInformationalVersion()}");
            Log.Verbose($"Environment Information:{Environment.NewLine}" +
                        $"  {string.Join($"{Environment.NewLine}  ", EnvironmentHelper.SafelyGetEnvironmentInformation())}");

            ProxyInitializer.InitializeDefaultProxy();

            try
            {
                if (command == null)
                {
                    return PrintHelp(PluginUtils.GetFirstArgument(args));
                }

                return command.Execute(args.Skip(1).ToArray());
            }
            catch (Exception ex)
            {
                return ConsoleFormatter.PrintError(ex);
            }
        }

        private int PrintHelp(string action)
        {
            Console.WriteLine("Help");
            return -2;
//            helpCommand.HelpWasAskedFor = false;
//            return helpCommand.Execute(new[] { action });
        }

        public static void EnableAllSecurityProtocols()
        {
            // TLS1.2 was required to access GitHub apis as of 22 Feb 2018. 
            // https://developer.github.com/changes/2018-02-01-weak-crypto-removal-notice/

            // TLS1.1 and below was discontinued on MavenCentral as of 18 June 2018
            //https://central.sonatype.org/articles/2018/May/04/discontinue-support-for-tlsv11-and-below/

            var securityProcotolTypes =
#if !NETCOREAPP2_0
                SecurityProtocolType.Ssl3 |
#endif
                SecurityProtocolType.Tls;

            if (Enum.IsDefined(typeof(SecurityProtocolType), 768))
                securityProcotolTypes = securityProcotolTypes | (SecurityProtocolType)768;

            if (Enum.IsDefined(typeof(SecurityProtocolType), 3072))
            {
                securityProcotolTypes = securityProcotolTypes | (SecurityProtocolType)3072;
            }
            else
            {
                Log.Verbose($"TLS1.2 is not supported, this means that some outgoing connections to third party endpoints will not work as they now only support TLS1.2.{Environment.NewLine}This includes GitHub feeds and Maven feeds.");
            }

            ServicePointManager.SecurityProtocol = securityProcotolTypes;
        }
    }
}
