using System;
using System.Collections.Generic;
using Autofac;
using Autofac.Core;
using Calamari.Commands.Support;
using Calamari.Deployment.Conventions;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Calamari.Shared;
using Calamari.Shared.Commands;
using Calamari.Shared.FileSystem;
using Octostache;
using Org.BouncyCastle.Crypto.Tls;
using IConvention = Calamari.Shared.Commands.IConvention;

namespace Calamari.Commands
{
    

    public class CalamariExecutionContext : IExecutionContext
    {
        public VariableDictionary Variables { get; set; }
        public string PackageFilePath { get;set; }
        
        
        
        public DeploymentWorkingDirectory CurrentDirectoryProvider { get; set; }

        public string CurrentDirectory => CurrentDirectoryProvider == DeploymentWorkingDirectory.StagingDirectory ?
            (string.IsNullOrWhiteSpace(StagingDirectory) ? Environment.CurrentDirectory : StagingDirectory)
            : CustomDirectory;


        /// <summary>
        /// Gets the directory that Tentacle extracted the package to.
        /// </summary>
        public string StagingDirectory
        {
            get => Variables.Get(SpecialVariables.OriginalPackageDirectoryPath);
            set => Variables.Set(SpecialVariables.OriginalPackageDirectoryPath, value);
        }
        
        /// <summary>
        /// Gets the custom installation directory for this package, as selected by the user.
        /// If the user didn't choose a custom directory, this will return <see cref="StagingDirectory"/> instead.
        /// </summary>
        public string CustomDirectory
        {
            get
            {
                var custom = Variables.Get(SpecialVariables.Package.CustomInstallationDirectory);
                return string.IsNullOrWhiteSpace(custom) ? StagingDirectory : custom;
            }
        }
    }


    public class FeaturesList : IFeaturesList 
    {
        private readonly Container container;
        public List<IFeature> Features = new List<IFeature>();
        
        public FeaturesList(Container container)
        {
            this.container = container;
        }
        
        public IFeaturesList Add(IFeature instance)
        {
            Features.Add(instance);
            return this;
        }

        public IFeaturesList Add<TFeature>() where TFeature : IFeature
        {
            return Add((IFeature) container.Resolve(typeof(TFeature)));
        }
    }
    
    public class CommandBuilder: ICommandBuilder
    {
        private readonly Container container;
        private readonly FeaturesList featuresList;
        public List<Action<IExecutionContext>> ConventionSteps = new List<Action<IExecutionContext>>();

        public CommandBuilder(Container container)
        {
            this.container = container;
            featuresList = new FeaturesList(container);
        }

        public bool UsesDeploymentJournal { get; set; }
        
        public IFeaturesList Features => featuresList;

        public ICommandBuilder AddContributeEnvironmentVariables()
        {
            return AddConvention<ContributeEnvironmentVariablesConvention>();
        }

        public ICommandBuilder AddLogVariables()
        {
            return AddConvention<LogVariablesConvention>();
        }

        public ICommandBuilder AddExtractPackageToStagingDirectory()
        {
            return AddConvention<ExtractPackageToStagingDirectoryConvention>();
        }

        public ICommandBuilder AddExtractPackageToApplicationDirectory()
        {
            return AddConvention<ExtractPackageToApplicationDirectoryConvention>();
        }

        public ICommandBuilder AddConfiguredScriptConvention(string stage)
        {
            return AddConvention((ctx) =>
            {
                var fileSystem = container.Resolve<ICalamariFileSystem>();
                var scriptEngine = container.Resolve<CombinedScriptEngine>();
                (new ConfiguredScriptConvention(stage, fileSystem, scriptEngine)).Run(ctx);
            });
        }

        public ICommandBuilder AddPackagedScriptConvention(string stage)
        {
            return AddConvention((ctx) =>
            {
                var fileSystem = container.Resolve<ICalamariFileSystem>();
                var scriptEngine = container.Resolve<CombinedScriptEngine>();
                (new PackagedScriptConvention(stage, fileSystem, scriptEngine)).Run(ctx);
            });
        }

        public ICommandBuilder AddSubsituteInFiles()
        {
            return AddConvention<SubstituteInFilesConvention>();
        }

        public ICommandBuilder AddConfigurationTransform()
        {
            return AddConvention<ConfigurationTransformsConvention>();
        }

        public ICommandBuilder AddConfigurationVariables()
        {
            return AddConvention<ConfigurationVariablesConvention>();
        }

        public ICommandBuilder AddJsonVariables()
        {
            return AddConvention<JsonConfigurationVariablesConvention>();
        }


        ICommandBuilder AddFeatureConvention(string stage)
        {
            return AddConvention((ctx) =>
            {
                var fileSystem = container.Resolve<ICalamariFileSystem>();
                var scriptEngine = container.Resolve<CombinedScriptEngine>();
                var embededResources = container.Resolve<ICalamariEmbeddedResources>();
                (new FeatureConvention(stage, featuresList.Features.ToArray(), fileSystem, scriptEngine, embededResources)).Run(ctx);
            });
        }

        
        
        public ICommandBuilder RunPreScripts()
        {
            //TODO: confirm only during Pre-Deploy the variable scripts are run before package scripts
            AddFeatureConvention(DeploymentStages.BeforePreDeploy);
            AddConfiguredScriptConvention(DeploymentStages.PreDeploy);
            AddPackagedScriptConvention(DeploymentStages.PreDeploy);
            AddFeatureConvention(DeploymentStages.AfterPreDeploy);
            return this;
        }

        public ICommandBuilder RunDeployScripts()
        {
            AddFeatureConvention(DeploymentStages.BeforeDeploy);
            AddPackagedScriptConvention(DeploymentStages.Deploy);
            AddConfiguredScriptConvention(DeploymentStages.Deploy);
            AddFeatureConvention(DeploymentStages.AfterDeploy);
            return this;
        }

        public ICommandBuilder RunPostScripts()
        {
            AddFeatureConvention(DeploymentStages.BeforePostDeploy);
            AddPackagedScriptConvention(DeploymentStages.PostDeploy);
            AddConfiguredScriptConvention(DeploymentStages.PostDeploy);
            AddFeatureConvention(DeploymentStages.AfterPostDeploy);
            return this;
        }


        public List<Action<IExecutionContext>> BuildRollbackScriptSteps()
        {
            // TODO: No variable based deploy failed?
            var cb = new CommandBuilder(container);
            cb.AddFeatureConvention(DeploymentStages.DeployFailed);
            cb.AddPackagedScriptConvention(DeploymentStages.DeployFailed);
            return cb.ConventionSteps;
        }

        public List<Action<IExecutionContext>> BuildCleanupScriptSteps()
        {
            var cb = new CommandBuilder(container);
            throw new NotImplementedException();
            
            /*if (deployment.Variables.GetFlag(SpecialVariables.DeleteScriptsOnCleanup, true))
            {
                DeleteScripts(deployment);
            }*/
            //return cb.ConventionSteps;
        }

        public ICommandBuilder AddConvention(Action<IExecutionContext> instance)
        {
            ConventionSteps.Add(instance);
            return this;
        }
        
        public ICommandBuilder AddConvention(IConvention instance)
        {
            return this.AddConvention(ctx => instance.Run(ctx));
        }

        public ICommandBuilder AddConvention<TConvention>() where TConvention : IConvention
        {
            return this.AddConvention((ctx) => ((IConvention)container.Resolve(typeof(TConvention))).Run(ctx));
        }
    }
}