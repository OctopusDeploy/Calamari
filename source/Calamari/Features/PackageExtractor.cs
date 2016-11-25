using System;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Extensibility;
using Calamari.Extensibility.Features;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;
using Calamari.Integration.Processes.Semaphores;
using IPackageExtractor = Calamari.Extensibility.Features.IPackageExtractor;

namespace Calamari.Features
{
    public class PackageExtractor : IPackageExtractor
    {
        
        private readonly ICalamariFileSystem fileSystem;
        private readonly IVariableDictionary variables;
        private readonly ISemaphoreFactory semaphore;

        public PackageExtractor(ICalamariFileSystem fileSystem, IVariableDictionary variables, ISemaphoreFactory semaphore)
        {
            this.fileSystem = fileSystem;
            this.variables = variables;
            this.semaphore = semaphore;
        }


        public string Extract(string package, PackageExtractionLocation extractionLocation)
        {
            ExtractPackageConvention extractPackage = null;
            switch (extractionLocation)
            {
                case PackageExtractionLocation.ApplicationDirectory:
                    extractPackage = new ExtractPackageToApplicationDirectoryConvention(new GenericPackageExtractor(), fileSystem, semaphore);
                    break;
                case PackageExtractionLocation.WorkingDirectory:
                    extractPackage = new ExtractPackageToWorkingDirectoryConvention(new GenericPackageExtractor(), fileSystem);
                    break;
                case PackageExtractionLocation.StagingDirectory:
                    extractPackage = new ExtractPackageToStagingDirectoryConvention(new GenericPackageExtractor(), fileSystem);
                    break;
                default:
                    throw new InvalidOperationException("Unknown extraction location: "+ extractionLocation);
            }

         

            var rd = new RunningDeployment(package, variables);
            extractPackage.Install(rd);

            return "";
        }
    }
}