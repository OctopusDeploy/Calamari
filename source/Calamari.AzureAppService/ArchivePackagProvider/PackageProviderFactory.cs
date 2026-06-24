using System;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.AzureAppService
{
    // Maps a file extension to its package provider (upload endpoint + sync/async mode). The one Calamari-owned
    // decision in an otherwise Azure-bound deploy, so it's unit-tested in PackageProviderFactoryFixture.
    public static class PackageProviderFactory
    {
        public static IPackageProvider GetProvider(string fileExtension,
                                                   ILog log,
                                                   ICalamariFileSystem fileSystem,
                                                   IVariables variables,
                                                   RunningDeployment deployment)
        {
            return fileExtension switch
                   {
                       ".zip" => new ZipPackageProvider(),
                       ".nupkg" => new NugetPackageProvider(),
                       ".war" => new JavaPackageProvider(log, fileSystem, variables, deployment, "/api/wardeploy"),
                       ".jar" => new JavaPackageProvider(log, fileSystem, variables, deployment, "/api/publish?type=jar"),
                       _ => throw new Exception("Unsupported archive type")
                   };
        }
    }
}