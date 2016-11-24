

using System;

namespace Calamari.Extensibility.Features
{

    public interface IPackageDeploymentFeature
    {
        void AfterDeploy(IVariableDictionary variables);
    }

    public interface IFeature
    {
        void Install(IVariableDictionary variables);
//        string Name { get; }
//        string Description { get; }
    }

    public interface IRollBack : IFeature
    {
        void Rollback(IVariableDictionary variables);
    }

    public interface IFeatureExecutionContext
    {
        IVariableDictionary Variables { get; }

        ILog Logger { get; }

        IFileSubstitution FileSubstitution { get; }

        IPackageExtractor PackageExtrator { get; }

        IScriptExecution ScriptExeceExecution { get; }
    }


    public interface IScriptExecution
    {
        void Invoke(string scriptFile, string scriptParameters);
    }


    public interface IPackageExtractor
    {
        string Extract(string package, PackageExtractionLocation extractionLocation);
    }

    public enum PackageExtractionLocation
    {
        ApplicationDirectory = 0,
        StagingDirectory = 1,
        WorkingDirectory = 2
    }


    public class FeatureAttribute : Attribute
    {
        public string Name { get; }
        public string Description { get; }
        public FeatureAttribute(string name, string description)
        {
            Name = name;
            Description = description;
        }
    }
}

