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
    
    public interface ICommandBuilder
    {
        //TODO: This should dissapear once all parameters are passed in via variable dictionary
        void PreExecution(Action<IOptionsBuilder, string> action);
        
        //TODO: This basically currently exists from RunScript so values can be validated... Be nice to not do this
        VariableDictionary Variables { get; }
        
        IFeaturesList Features { get; }
        
        bool UsesDeploymentJournal { get; set; }
        
        ICommandBuilder AddExtractPackageToStagingDirectory();
        ICommandBuilder AddExtractPackageToApplicationDirectory();

        ICommandBuilder AddSubsituteInFiles(Func<IExecutionContext, bool> predicate = null,
            Func<IExecutionContext, IEnumerable<string>> fileTargetFactory = null);
        ICommandBuilder AddConfigurationTransform();
        ICommandBuilder AddConfigurationVariables();
        ICommandBuilder AddJsonVariables();


        ICommandBuilder RunPreScripts();
        ICommandBuilder RunDeployScripts();
        ICommandBuilder RunPostScripts();


        ICommandBuilder AddConvention(Action<IExecutionContext> action);
        ICommandBuilder AddConvention(IConvention instance);
        ICommandBuilder AddConvention<TConvention>() where TConvention : IConvention;
    }
}