using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Calamari.Commands.Support;
using Calamari.Deployment.Conventions;
using Calamari.Extensibility;
using Calamari.Extensibility.Features;
using Calamari.Features;
using Calamari.Features.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.ServiceMessages;
using Calamari.Integration.Substitutions;
using Calamari.Util;
using System.Runtime.InteropServices;
using Calamari.Conventions.RunScript;
using Calamari.Deployment;
using Calamari.Integration.Packages;
using Calamari.Integration.Processes.Semaphores;
using IPackageExtractor = Calamari.Extensibility.Features.IPackageExtractor;
using SpecialVariables = Calamari.Shared.SpecialVariables;

namespace Calamari.Commands
{
    [Command("run-feature", Description = "Extracts and installs a deployment package")]
    public class RunFeatureCommand : Command
    {
        private string variablesFile;
        private string sensitiveVariablesFile;
        private string sensitiveVariablesPassword;
        private string featureName;

        public RunFeatureCommand()
        {
            Options.Add("feature=",
                "The name of the feature that will be loaded from available assembelies and invoked.",
                v => featureName = v);
            Options.Add("variables=", "Path to a JSON file containing variables.",
                v => variablesFile = Path.GetFullPath(v));
            Options.Add("sensitiveVariables=", "Password protected JSON file containing sensitive-variables.",
                v => sensitiveVariablesFile = v);
            Options.Add("sensitiveVariablesPassword=", "Password used to decrypt sensitive-variables.",
                v => sensitiveVariablesPassword = v);
        }


        internal int Execute(string featureName, IVariableDictionary variables)
        {
            variables.EnrichWithEnvironmentVariables();
            variables.LogVariables();


            var al = new AssemblyLoader();
            
            al.RegisterAssembly(typeof(RunScriptFeature).GetTypeInfo().Assembly);
            al.RegisterAssembly(typeof(DeployPackageFeature).GetTypeInfo().Assembly);

            var featureLocator = new FeatureLocator(al);
            Console.Write(al.Types.Count());

            var type = featureLocator.Locate(featureName);

            var container = CreateContainer(variables);


            var dpb = new DepencencyInjectionBuilder(container);

            var feature = (IFeature)dpb.BuildConvention(type);
            feature.Install(variables);
            return 0;
        }
        
        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);
            Guard.NotNullOrWhiteSpace(featureName, "No feature was specified. Please pass a value for the `--feature` option.");

            return Execute(featureName, new CalamariVariableDictionary(variablesFile, sensitiveVariablesFile, sensitiveVariablesPassword));
        }

        private static CalamariContainer CreateContainer(IVariableDictionary variables)
        {
            var container = new CalamariContainer();
            var filesystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();

            var commandLineRunner = new CommandLineRunner(new SplitCommandOutput(new ConsoleCommandOutput(), new ServiceMessageCommandOutput(variables)));
            var log = new LogWrapper();

            container.RegisterInstance<IPackageExtractor>(new PackageExtractor(filesystem, variables, SemaphoreFactory.Get()));
            container.RegisterInstance<IVariableDictionary>(variables);
            container.RegisterInstance<IScriptExecution>(new ScriptExecution(filesystem, new Integration.Scripting.CombinedScriptEngine(), commandLineRunner, variables));
            container.RegisterInstance<IFileSubstitution>(new FileSubstitution(filesystem, new FileSubstituter(filesystem), variables, log));
            container.RegisterInstance<ILog>(log);
            return container;
        }
    }

    public class PackageExtractor : IPackageExtractor
    {
        
        private readonly ICalamariFileSystem fileSystem;
        private readonly IVariableDictionary variables;
        private readonly ISemaphoreFactory semaphore;

        public PackageExtractor(ICalamariFileSystem fileSystem, IVariableDictionary variables, ISemaphoreFactory semaphore)
        {
            this.fileSystem = fileSystem;
            this.variables = variables;
            this.semaphore = semaphore;
        }


        public string Extract(string package, PackageExtractionLocation extractionLocation)
        {
            ExtractPackageConvention extractPackage = null;
            switch (extractionLocation)
            {
                case PackageExtractionLocation.ApplicationDirectory:
                    extractPackage = new ExtractPackageToApplicationDirectoryConvention(new GenericPackageExtractor(), fileSystem, semaphore);
                    break;
                case PackageExtractionLocation.WorkingDirectory:
                    extractPackage = new ExtractPackageToWorkingDirectoryConvention(new GenericPackageExtractor(), fileSystem);
                    break;
                case PackageExtractionLocation.StagingDirectory:
                    extractPackage = new ExtractPackageToStagingDirectoryConvention(new GenericPackageExtractor(), fileSystem);
                    break;
                default:
                    throw new InvalidOperationException("Unknown extraction location: "+ extractionLocation);
            }

         

            var rd = new RunningDeployment(package, variables);
            extractPackage.Install(rd);

            return "";
        }
    }

    

    public class FileSubstitution : IFileSubstitution
    {
        private readonly ICalamariFileSystem fileSystem;
        private readonly IFileSubstituter substituter;
        private readonly IVariableDictionary variableDictionary;
        private readonly ILog log;

        public FileSubstitution(ICalamariFileSystem fileSystem, IFileSubstituter substituter, IVariableDictionary variableDictionary, ILog log)
        {
            this.fileSystem = fileSystem;
            this.substituter = substituter;
            this.variableDictionary = variableDictionary;
            this.log = log;
        }

        public void PerformSubstitution(string sourceFile)
        {
            if (!variableDictionary.GetFlag(SpecialVariables.Package.SubstituteInFilesEnabled))
                return;

            if (!fileSystem.FileExists(sourceFile))
            {
                log.WarnFormat($"The file '{sourceFile}' could not be found for variable substitution.");
                return;
            }
            log.Info($"Performing variable substitution on '{sourceFile}'");
            substituter.PerformSubstitution(sourceFile, variableDictionary);
        }
    }
}