using System.IO;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Integration.Iis;

namespace Calamari.Deployment.Conventions
{
    public class LegacyIisWebSiteConvention : IInstallConvention
    {
        readonly ICalamariFileSystem fileSystem;
        readonly IInternetInformationServer iis;

        public LegacyIisWebSiteConvention(ICalamariFileSystem fileSystem, IInternetInformationServer iis)
        {
            this.fileSystem = fileSystem;
            this.iis = iis;
        }

        public void Install(RunningDeployment deployment)
        {
            if (!deployment.Variables.GetFlag(SpecialVariables.Package.UpdateIisWebsite))
                return;

            var iisSiteName = deployment.Variables.Get(SpecialVariables.Package.UpdateIisWebsiteName);
            if (string.IsNullOrWhiteSpace(iisSiteName))
            {
                iisSiteName = deployment.Variables.Get(PackageVariables.PackageId);
            }

            var webRoot = GetRootMostDirectoryContainingWebConfig(deployment);
            if (webRoot == null)
                throw new CommandException("A web.config file was not found, so no IIS configuration will be performed. To turn off this feature, use the 'Configure features' link in the deployment step configuration to disable IIS updates.");

            // In situations where the IIS version cannot be correctly determined automatically,
            // this variable can be set to force IIS6 compatibility.
            var legacySupport = LegacySupport(deployment);

            var updated = iis.OverwriteHomeDirectory(iisSiteName, webRoot, legacySupport);

            if (!updated)
                throw new CommandException(
                    string.Format(
                        "Could not find an IIS website or virtual directory named '{0}' on the local machine. You need to create the site and/or virtual directory manually. To turn off this feature, use the 'Configure features' link in the deployment step configuration to disable IIS updates.", 
                        iisSiteName));

            Log.Info("The IIS website named '{0}' has had its path updated to: '{1}'", iisSiteName, webRoot);
        }

        static bool LegacySupport(RunningDeployment deployment)
        {
            var legacySupport = deployment.Variables.GetFlag(SpecialVariables.UseLegacyIisSupport);

            // IIS7 was shipped in Windows Server 2008 and so this should not be getting hit by ANY customers (i.e. 2003 is deprecated)
            if (!legacySupport)
                return false;

            // We were previously never warning of its imminent removal so
            // provide a final legacy support capability with another big warning
            // This will provide us some cover for later removal.
            var forceLegacySupport = deployment.Variables.GetFlag(SpecialVariables.UseLegacyIisSupportForce);
            if (!forceLegacySupport)
                throw new CommandException($"Support for IIS6 is no longer supported.\r\n"
                                           + $"Remove the {SpecialVariables.UseLegacyIisSupportForce} variable and ensure you are targeting IIS7+.");

            Log.Warn($"LegacyIIS support confirmed.\r\n"
                     + $"Support for IIS6 is no longer supported.\r\n"
                     + $"Remove the `{SpecialVariables.UseLegacyIisSupport}` and `{SpecialVariables.UseLegacyIisSupportForce}` variables and ensure you are targeting IIS7+.\r\n"
                     + $"This capability will be very shortly removed without further warning.");
            return true;

        }

        string GetRootMostDirectoryContainingWebConfig(RunningDeployment deployment)
        {
            // Optimize for most common case.
            if (fileSystem.FileExists(Path.Combine(deployment.CurrentDirectory, "Web.config")))
            {
                return deployment.CurrentDirectory;
            }

            // Find all folders under package root and sort them by depth
            var dirs = fileSystem.EnumerateDirectoriesRecursively(deployment.CurrentDirectory).ToList();
            return dirs.OrderBy(x => x.Count(c => c == '\\')).FirstOrDefault(dir => fileSystem.FileExists(Path.Combine(dir, "Web.config")));
        }
    }
}