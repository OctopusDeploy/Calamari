using System;
using System.IO;
using Calamari.Extensibility.Features;
using Calamari.Extensibility.IIS.FileSystem;
using System.Linq;

namespace Calamari.Extensibility.IIS
{
    [Feature("IISDeployment", "I Am A Run Script")]
    public class IISDeploymentFeature : IPackageDeploymentFeature
    {
        readonly ICalamariFileSystem fileSystem;
        readonly IInternetInformationServer iis;
        private readonly ILog log;

        public IISDeploymentFeature(ICalamariFileSystem fileSystem, IInternetInformationServer iis, ILog log)
        {
            this.fileSystem = fileSystem;
            this.iis = iis;
            this.log = log;
        }


        public void AfterDeploy(IVariableDictionary variables)
        {
            throw new NotImplementedException();
        }

        public void AfterDeploy2(IVariableDictionary variables, string currentDirectory)
        {
            //////////////////////FIX ME///////////////////////
            if (!variables.GetFlag(SpecialVariables.Package.UpdateIisWebsite))
                return;

            var iisSiteName = variables.Get(SpecialVariables.Package.UpdateIisWebsiteName);
            if (string.IsNullOrWhiteSpace(iisSiteName))
            {
                iisSiteName = variables.Get(SpecialVariables.Package.NuGetPackageId);
            }

            var webRoot = GetRootMostDirectoryContainingWebConfig(currentDirectory);
            if (webRoot == null)
                throw new CommandException("A web.config file was not found, so no IIS configuration will be performed. To turn off this feature, use the 'Configure features' link in the deployment step configuration to disable IIS updates.");

            // In situations where the IIS version cannot be correctly determined automatically,
            // this variable can be set to force IIS6 compatibility.
            var legacySupport = variables.GetFlag(SpecialVariables.UseLegacyIisSupport);

            var updated = iis.OverwriteHomeDirectory(iisSiteName, webRoot, legacySupport);

            if (!updated)
                throw new CommandException($"Could not find an IIS website or virtual directory named '{iisSiteName}' on the local machine. You need to create the site and/or virtual directory manually. To turn off this feature, use the 'Configure features' link in the deployment step configuration to disable IIS updates.");

            log.Info($"The IIS website named '{iisSiteName}' has had its path updated to: '{webRoot}'");
        }

        string GetRootMostDirectoryContainingWebConfig(string currentDirectory)
        {
            // Optimize for most common case.
            if (fileSystem.FileExists(Path.Combine(currentDirectory, "Web.config")))
            {
                return currentDirectory;
            }

            // Find all folders under package root and sort them by depth
            var dirs = fileSystem.EnumerateDirectoriesRecursively(currentDirectory).ToList();
            return dirs.OrderBy(x => x.Count(c => c == '\\')).FirstOrDefault(dir => fileSystem.FileExists(Path.Combine(dir, "Web.config")));
        }        
    }
}
