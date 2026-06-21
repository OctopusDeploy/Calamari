using System;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.AzureAppService
{
    /// <summary>
    /// Selects the <see cref="IPackageProvider" /> for a package based on its file extension. This mapping
    /// (extension to packaging strategy, Kudu upload endpoint and sync/async deployment) is the only
    /// Calamari-owned decision in the otherwise Azure-bound deployment, so it is unit tested in
    /// PackageProviderFactoryFixture without a real Azure connection.
    /// </summary>
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