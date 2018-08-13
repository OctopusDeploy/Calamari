using System;
using System.Collections.Generic;
using Autofac;
using Autofac.Core;
using Calamari.Deployment.Conventions;
using Calamari.Deployment.Journal;
using Calamari.Integration.Packages;
using Calamari.Integration.Scripting;
using Calamari.Shared;
using Calamari.Shared.Commands;
using Calamari.Shared.FileSystem;
using Octostache;
using IConvention = Calamari.Shared.Commands.IConvention;

namespace Calamari.Commands
{
    
    public class CalamariExecutionContext : IExecutionContext
    {
        public VariableDictionary Variables { get; set; }
        public string PackageFilePath { get;set; }
        
        
        public CalamariExecutionContext(string packageFilePath, VariableDictionary variables)
        {
            PackageFilePath = packageFilePath;
            Variables = variables;
        }
        
        public CalamariExecutionContext()
        {
        }
        
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
        private readonly IContainer container;
        public List<IFeature> Features = new List<IFeature>();
        
        public FeaturesList(IContainer container)
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
    
    public class DeploymentStrategyBuilder: IDeploymentStrategyBuilder
    {
        private readonly IContainer container;
        private readonly FeaturesList featuresList;
        readonly List<Action<IExecutionContext>> conventionSteps = new List<Action<IExecutionContext>>();

        public DeploymentStrategyBuilder(IContainer container)
        {
            this.container = container;
            featuresList = new FeaturesList(container);
        }


        public IEnumerable<Action<IExecutionContext>> BuildConventionSteps(IDeploymentJournal journal)
        {
            yield return (ctx) => new ContributeEnvironmentVariablesConvention().Run(ctx);
            if (journal != null)
            {
                yield return (ctx) => new ContributePreviousInstallationConvention(journal).Run(ctx);
                yield return (ctx) => new ContributePreviousSuccessfulInstallationConvention(journal).Run(ctx);
            }
            yield return (ctx) => new LogVariablesConvention().Run(ctx);
            
            if (journal != null)
            {
                yield return (ctx) => new AlreadyInstalledConvention(journal).Run(ctx);
            }

            foreach (var action in conventionSteps)
            {
                yield return action;
            }
        }

        public bool UsesDeploymentJournal { get; set; }

        public PreExecutionHandler PreExecution { get; set; }
        public VariableDictionary Variables { get; set; }
        public IFeaturesList Features => featuresList;

        public IDeploymentStrategyBuilder AddContributeEnvironmentVariables()
        {
            return AddConvention<ContributeEnvironmentVariablesConvention>();
        }

        public IDeploymentStrategyBuilder AddLogVariables()
        {
            return AddConvention<LogVariablesConvention>();
        }

        public IDeploymentStrategyBuilder AddExtractPackageToStagingDirectory()
        {
            return AddConvention<ExtractPackageToStagingDirectoryConvention>();
        }

        public IDeploymentStrategyBuilder AddExtractPackageToApplicationDirectory()
        {
            return AddConvention<ExtractPackageToApplicationDirectoryConvention>();
        }

    
        public IDeploymentStrategyBuilder AddSubsituteInFiles(
            Func<IExecutionContext, bool> predicate = null,
            Func<IExecutionContext, IEnumerable<string>> fileTargetFactory = null)
        {
            return AddConvention(ctx =>
            {
                new SubstituteInFilesConvention(container.Resolve<ICalamariFileSystem>(),
                    container.Resolve<IFileSubstituter>(),
                    predicate,
                    fileTargetFactory).Run(ctx);
            });
        }

        public IDeploymentStrategyBuilder AddConfigurationTransform()
        {
            return AddConvention<ConfigurationTransformsConvention>();
        }

        public IDeploymentStrategyBuilder AddConfigurationVariables()
        {
            return AddConvention<ConfigurationVariablesConvention>();
        }

        public IDeploymentStrategyBuilder AddJsonVariables()
        {
            return AddConvention<JsonConfigurationVariablesConvention>();
        }

        public IDeploymentStrategyBuilder AddStageScriptPackages(bool forceExtract = false)
        {
            return AddConvention(ctx =>
            {
                new StageScriptPackagesConvention(container.Resolve<ICalamariFileSystem>(),
                    container.Resolve<IGenericPackageExtractor>(),
                    forceExtract).Run(ctx);
            });
        }


        void AddConfiguredScriptConvention(string stage)
        {
            AddConvention((ctx) =>
            {
                var fileSystem = container.Resolve<ICalamariFileSystem>();
                var scriptEngine = container.Resolve<CombinedScriptEngine>();
                (new ConfiguredScriptConvention(stage, fileSystem, scriptEngine)).Run(ctx);
            });
        }

        void AddPackagedScriptConvention(string stage)
        {
            AddConvention((ctx) =>
            {
                var fileSystem = container.Resolve<ICalamariFileSystem>();
                var scriptEngine = container.Resolve<CombinedScriptEngine>();
                (new PackagedScriptConvention(stage, fileSystem, scriptEngine)).Run(ctx);
            });
        }
        
        void AddFeatureConvention(string stage)
        {
            AddConvention((ctx) =>
            {
                var fileSystem = container.Resolve<ICalamariFileSystem>();
                var scriptEngine = container.Resolve<CombinedScriptEngine>();
                var embededResources = container.Resolve<ICalamariEmbeddedResources>();
                (new FeatureConvention(stage, featuresList.Features.ToArray(), fileSystem, scriptEngine, embededResources)).Run(ctx);
            });
        }
        
        public IDeploymentStrategyBuilder RunPreScripts()
        {
            //TODO: confirm only during Pre-Deploy the variable scripts are run before package scripts
            AddFeatureConvention(DeploymentStages.BeforePreDeploy);
            AddConfiguredScriptConvention(DeploymentStages.PreDeploy);
            AddPackagedScriptConvention(DeploymentStages.PreDeploy);
            AddFeatureConvention(DeploymentStages.AfterPreDeploy);
            return this;
        }

        public IDeploymentStrategyBuilder RunDeployScripts()
        {
            AddFeatureConvention(DeploymentStages.BeforeDeploy);
            AddPackagedScriptConvention(DeploymentStages.Deploy);
            AddConfiguredScriptConvention(DeploymentStages.Deploy);
            AddFeatureConvention(DeploymentStages.AfterDeploy);
            return this;
        }

        public IDeploymentStrategyBuilder RunPostScripts()
        {
            AddFeatureConvention(DeploymentStages.BeforePostDeploy);
            AddPackagedScriptConvention(DeploymentStages.PostDeploy);
            AddConfiguredScriptConvention(DeploymentStages.PostDeploy);
            AddFeatureConvention(DeploymentStages.AfterPostDeploy);
            return this;
        }


        public List<Action<IExecutionContext>> BuildRollbackScriptSteps()
        {
            var fileSystem = container.Resolve<ICalamariFileSystem>();
            var scriptEngine = container.Resolve<CombinedScriptEngine>();
            var embededResources = container.Resolve<ICalamariEmbeddedResources>();
            
            // TODO: No variable based deploy failed?
            return new List<Action<IExecutionContext>>()
            {
                (ctx) =>
                {
                  
                    (new FeatureConvention(DeploymentStages.DeployFailed, featuresList.Features.ToArray(), fileSystem,
                        scriptEngine,
                        embededResources)).Run(ctx);
                },
                (ctx) =>
                {
                    (new RollbackScriptConvention(DeploymentStages.DeployFailed, fileSystem, scriptEngine)).Run(ctx);
                }
            };
        }

        public List<Action<IExecutionContext>> BuildCleanupScriptSteps()
        {
            return new List<Action<IExecutionContext>>()
            {
                (ctx) =>
                {
                    var fileSystem = container.Resolve<ICalamariFileSystem>();
                    var scriptEngine = container.Resolve<CombinedScriptEngine>();
                    (new RollbackScriptConvention(DeploymentStages.DeployFailed, fileSystem, scriptEngine)).Cleanup(ctx);
                }
            };
        }

        
        // Custom-defined conventions
        public IDeploymentStrategyBuilder AddConvention(Action<IExecutionContext> instance)
        {
            conventionSteps.Add(instance);
            return this;
        }
        
        public IDeploymentStrategyBuilder AddConvention(IConvention instance)
        {
            return this.AddConvention(ctx => instance.Run(ctx));
        }

        public IDeploymentStrategyBuilder AddConvention<TConvention>() where TConvention : IConvention
        {
            return this.AddConvention((ctx) => ((IConvention)container.Resolve(typeof(TConvention))).Run(ctx));
        }
    }
}