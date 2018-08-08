using System;

namespace Calamari.Shared.Commands
{

    public interface IFeaturesList
    {
        IFeaturesList Add(IFeature instance);
        IFeaturesList Add<TFeature>() where TFeature : IFeature;
    }
    
    public interface ICommandBuilder
    {
        
        IFeaturesList Features { get; }
        
        ICommandBuilder AddContributeEnvironmentVariables();
        ICommandBuilder AddLogVariables();
        
        ICommandBuilder AddExtractPackageToStagingDirectory();
        ICommandBuilder AddExtractPackageToApplicationDirectory();
        
        ICommandBuilder AddConfiguredScriptConvention(string stage);
        ICommandBuilder AddPackagedScriptConvention(string stage);
        ICommandBuilder AddSubsituteInFiles();
        ICommandBuilder AddConfigurationTransform();
        ICommandBuilder AddConfigurationVariables();
        ICommandBuilder AddJsonVariables();


        ICommandBuilder RunPreScripts();
        ICommandBuilder RunDeployScripts();
        ICommandBuilder RunPostScripts();
        
        
        ICommandBuilder AddConvention(Action<IExecutionContext> instance)
        ICommandBuilder AddConvention(IConvention instance);
        ICommandBuilder AddConvention<TConvention>() where TConvention : IConvention;
    }
}