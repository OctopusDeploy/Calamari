using System;
using System.Collections.Generic;
using Octopus.CoreUtilities;

namespace Calamari.Testing.Tools
{
    //TODO: This is pulled in from Sashimi.Server.Contracts as an attempt to run Calamari Commands with Tools. Ideally this wouldn't be duplicated.
    public interface IDeploymentTool
    {
        string Id { get; }
        Maybe<string> SubFolder { get; }
        bool AddToPath { get; }
        Maybe<string> ToolPathVariableToSet { get; }
        string[] SupportedPlatforms { get; }
        Maybe<DeploymentToolPackage> GetCompatiblePackage(string platform);
    }

    public class DeploymentToolPackage
    {
        public DeploymentToolPackage(IDeploymentTool tool, string id)
        {
            Tool = tool;
            Id = id;
            BootstrapperModulePaths = new string[0];
        }

        public DeploymentToolPackage(IDeploymentTool tool, string id, IReadOnlyList<string> modulePaths)
        {
            Tool = tool;
            Id = id;
            BootstrapperModulePaths = modulePaths;
        }

        public IDeploymentTool Tool { get; }
        public string Id { get; }
        public IReadOnlyList<string> BootstrapperModulePaths { get; set; }
    }
}