using Octostache;

namespace Calamari.Shared.Commands
{

    
    public interface IExecutionContext
    {
        VariableDictionary Variables { get; }
        string CurrentDirectory { get; }
        
        //TODO: See if we can remove this
        string PackageFilePath { get; }
        
        
        
        
        
        string StagingDirectory { get; }
        string CustomDirectory { get; }
        DeploymentWorkingDirectory CurrentDirectoryProvider { get; set; }
    
    }
    
    public enum DeploymentWorkingDirectory
    {
        StagingDirectory,
        CustomDirectory
    }
    
    
    public interface IFeature
    {
        string Name { get; }

        string DeploymentStage { get; }

        void Execute(IExecutionContext deployment);
    }
}