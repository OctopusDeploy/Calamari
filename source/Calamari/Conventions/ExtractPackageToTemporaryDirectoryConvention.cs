namespace Octopus.Deploy.PackageInstaller
{
    public class ExtractPackageToTemporaryDirectoryConvention : IInstallConvention
    {
        public void Install(RunningDeployment deployment)
        {
            // Get the package file
            // Decide where to extract it (a variable for the root drive must be passed in)
            // Extract it using System.IO.Packaging
            // Store the result as a variable
        }
    }
}