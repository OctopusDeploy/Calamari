using System;
using System.Collections.Generic;
using Autofac;
using Autofac.Core;
using Calamari.Deployment.Conventions;
using Calamari.Deployment.Journal;
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
    
    public class CommandBuilder: ICommandBuilder
    {
        private readonly IContainer container;
        private readonly FeaturesList featuresList;
        readonly List<Action<IExecutionContext>> conventionSteps = new List<Action<IExecutionContext>>();

        public CommandBuilder(IContainer container)
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
        public bool PerformFreespaceCheck { get; set; }
        
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

        public ICommandBuilder AddConvention(Action<IExecutionContext> instance)
        {
            conventionSteps.Add(instance);
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