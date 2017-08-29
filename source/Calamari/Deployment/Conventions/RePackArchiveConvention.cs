using System;
using System.IO;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;
using Calamari.Integration.Packages.Java;
using Calamari.Integration.Processes;

namespace Calamari.Java.Deployment.Conventions
{
    public class RePackArchiveConvention : IInstallConvention
    {
        readonly ICalamariFileSystem fileSystem;
        readonly IPackageExtractor packageExtractor;
        readonly JarTool jarTool;

        public RePackArchiveConvention(ICalamariFileSystem fileSystem, IPackageExtractor packageExtractor, ICommandLineRunner commandLineRunner)
        {
            this.fileSystem = fileSystem;
            this.packageExtractor = packageExtractor;
            this.jarTool = new JarTool(commandLineRunner, fileSystem); 
        }

        public void Install(RunningDeployment deployment)
        {
            if (deployment.Variables.GetFlag(SpecialVariables.Action.Java.JavaArchiveExtractionDisabled, false))
            {
                return;
            }                

            var packageMetadata = packageExtractor.GetMetadata(deployment.PackageFilePath);

            deployment.Variables.Set(
                SpecialVariables.RepackedArchiveLocation,
                deployment.Variables.IsSet(SpecialVariables.Package.CustomPackageInstallationDirectory)
                    ? BuildAndCopy(deployment, packageMetadata)
                    : Build(deployment, packageMetadata));
        }

        protected string Build(RunningDeployment deployment, PackageMetadata packageMetadata)
        {            
            var applicationDirectory = ApplicationDirectory.GetApplicationDirectory(
                packageMetadata, 
                deployment.Variables, 
                fileSystem);
            var targetFileName = $"{packageMetadata.Id}.{packageMetadata.Version}{packageMetadata.FileExtension}";
            var targetFilePath = Path.Combine(applicationDirectory, targetFileName);
                        
            var stagingDirectory = deployment.CurrentDirectory;

            jarTool.CreateJar(stagingDirectory, targetFilePath);
            Log.Info($"Re-packaging archive: '{targetFilePath}'");

            return targetFilePath;
        }

        protected string BuildAndCopy(RunningDeployment deployment, PackageMetadata packageMetadata)
        {
            var targetDirectory = deployment.Variables.Get(SpecialVariables.Package.CustomPackageInstallationDirectory);

            var targetFileName = deployment.Variables.Get(
                SpecialVariables.Package.CustomPackageFileName,
                $"{packageMetadata.Id}.{packageMetadata.Version}{packageMetadata.FileExtension}");
                      
            var targetFilePath = Path.Combine(targetDirectory, targetFileName);

            /*
               Start by building up the temporary file. We do this because often the directory that
               this file will be copied into is being monitored by an application server, and we
               don't want to build up a file that might be half deployed.
            */
            var intermediatePath = Path.Combine(Path.GetTempPath(), targetFileName + "-" + Guid.NewGuid().ToString()); 
            
            /*
                The odds that this file exists are small, but clear it out anyway if it
                already exists.
            */
            if (File.Exists(intermediatePath))
            {
                File.Delete(intermediatePath);
            }
            
            var stagingDirectory = deployment.CurrentDirectory;
            Log.Info($"Re-packaging archive: '{intermediatePath}'");

            jarTool.CreateJar(stagingDirectory, intermediatePath);
            
            /*
                Now copy the temporary file to the destination location, removing any exiting
                files first.
            */
            if (File.Exists(targetFilePath))
            {
                File.Delete(targetFilePath);
            }
            
            File.Copy(intermediatePath, targetFilePath);
            File.Delete(intermediatePath);

            return targetFilePath;
        }
    }
}