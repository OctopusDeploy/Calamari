using System;
using System.Collections.Generic;

namespace Sashimi.Server.Contracts.DeploymentTools
{
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