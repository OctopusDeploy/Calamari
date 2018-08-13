using System;
using System.Collections.Generic;
using Octostache;

namespace Calamari.Shared.Commands
{

    public interface IFeaturesList
    {
        IFeaturesList Add(IFeature instance);
        IFeaturesList Add<TFeature>() where TFeature : IFeature;
    }
    
    //TODO: Remove this once all paramaters are passed in via variable dictionary
    public delegate void PreExecutionHandler(IOptionsBuilder opts, VariableDictionary variables);
    
    public interface IDeploymentStrategyBuilder
    {
        //TODO: This should dissapear once all parameters are passed in via variable dictionary
        PreExecutionHandler PreExecution { get; set; }
        
        //TODO: This basically currently exists from RunScript so values can be validated... Be nice to not do this
        VariableDictionary Variables { get; }
        
        IFeaturesList Features { get; }
        
        bool UsesDeploymentJournal { get; set; }
        
        IDeploymentStrategyBuilder AddExtractPackageToStagingDirectory();
        IDeploymentStrategyBuilder AddExtractPackageToApplicationDirectory();

        IDeploymentStrategyBuilder AddSubsituteInFiles(Func<IExecutionContext, bool> predicate = null,
            Func<IExecutionContext, IEnumerable<string>> fileTargetFactory = null);
        IDeploymentStrategyBuilder AddConfigurationTransform();
        IDeploymentStrategyBuilder AddConfigurationVariables();
        IDeploymentStrategyBuilder AddJsonVariables();
        IDeploymentStrategyBuilder AddStageScriptPackages(bool forceExtract = false);


        IDeploymentStrategyBuilder RunPreScripts();
        IDeploymentStrategyBuilder RunDeployScripts();
        IDeploymentStrategyBuilder RunPostScripts();


        IDeploymentStrategyBuilder AddConvention(Action<IExecutionContext> action);
        IDeploymentStrategyBuilder AddConvention(IConvention instance);
        IDeploymentStrategyBuilder AddConvention<TConvention>() where TConvention : IConvention;
    }
}