namespace Calamari.Extensibility.Features
{
    public interface IPackageDeploymentFeature
    {
        void AfterDeploy(IVariableDictionary variables);
        void AfterDeploy2(IVariableDictionary variables, string currentDirectory);
    }
}