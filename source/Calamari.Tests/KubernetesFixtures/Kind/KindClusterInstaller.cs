using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Serilog;

namespace Calamari.Tests.KubernetesFixtures.Kind
{
    public class KindClusterInstaller
    {
        readonly string clusterName;
        readonly string kubeConfigName;

        readonly InstallTools installTools;
        readonly TemporaryDirectory temporaryDirectory;
        readonly ILog logger;

        public string KubeConfigPath => Path.Combine(temporaryDirectory.DirectoryPath, kubeConfigName);
        public string ClusterName => clusterName;

        public KindClusterInstaller(InstallTools installTools, TemporaryDirectory temporaryDirectory, ILog logger)
        {
            this.installTools = installTools;
            this.temporaryDirectory = temporaryDirectory;
            this.logger = logger;

            clusterName = $"tentacleint-{DateTime.Now:yyyyMMddhhmmss}";
            kubeConfigName = $"{clusterName}.config";
        }

        public async Task InstallCluster()
        {
            var sw = new Stopwatch();
            sw.Restart();
            
            
            
            
            var result = SilentProcessRunner.ExecuteCommand(
                                                              installTools.KindExecutable,
                                                              //we give the cluster a unique name
                                                              $"create cluster --name={clusterName} --kubeconfig=\"{kubeConfigName}\"",
                                                              temporaryDirectory.DirectoryPath,
                                                              logger.Debug,
                                                              logger.Information,
                                                              logger.Error,
                                                              CancellationToken.None);

            sw.Stop();
            

            if (result.ExitCode != 0)
            {
                logger.Error("Failed to create Kind Kubernetes cluster {ClusterName}", clusterName);
                throw new InvalidOperationException($"Failed to create Kind Kubernetes cluster {clusterName}");
            }

            logger.Information("Test cluster kubeconfig path: {Path:l}", KubeConfigPath);

            logger.Information("Created Kind Kubernetes cluster {ClusterName} in {ElapsedTime}", clusterName, sw.Elapsed);

            await SetLocalhostRouting();

            await InstallNfsCsiDriver();
        }

        async Task SetLocalhostRouting()
        {
            var filename = CalamariEnvironment.IsRunningOnNix ? "linux-network-routing.yaml" : "docker-desktop-network-routing.yaml";

            var manifestFilePath = await WriteFileToTemporaryDirectory(filename, "manifest.yaml");

            var sb = new StringBuilder();
            var sprLogger = new LoggerConfiguration()
                            .WriteTo.Logger(logger)
                            .WriteTo.StringBuilder(sb)
                            .CreateLogger();

            var exitCode = SilentProcessRunner.ExecuteCommand(
                                                              kubeCtlPath,
                                                              //we give the cluster a unique name
                                                              $"apply -n default -f \"{manifestFilePath}\" --kubeconfig=\"{KubeConfigPath}\"",
                                                              tempDir.DirectoryPath,
                                                              sprLogger.Debug,
                                                              sprLogger.Information,
                                                              sprLogger.Error,
                                                              CancellationToken.None);

            if (exitCode != 0)
            {
                logger.Error("Failed to apply localhost routing to cluster {ClusterName}", clusterName);
                throw new InvalidOperationException($"Failed to apply localhost routing to cluster {clusterName}. Logs: {sb}");
            }
        }

        async Task<string> WriteFileToTemporaryDirectory(string resourceFileName, string? outputFilename = null)
        {
            await using var resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStreamFromPartialName(resourceFileName);

            var filePath = Path.Combine(tempDir.DirectoryPath, outputFilename ?? resourceFileName);
            await using var file = File.Create(filePath);

            resourceStream.Seek(0, SeekOrigin.Begin);
            await resourceStream.CopyToAsync(file);

            return filePath;
        }

        async Task InstallNfsCsiDriver()
        {
            await Task.CompletedTask;
            //we need to perform a repo update in helm first
            // var exitCode = SilentProcessRunner.ExecuteCommand(
            //     helmPath,
            //     "repo update",
            //     tempDir.DirectoryPath,
            //     logger.Debug,
            //     logger.Information,
            //     logger.Error,
            //     CancellationToken.None);

            var installArgs = BuildNfsCsiDriverInstallArguments();
        
            var sb = new StringBuilder();
            var sprLogger = new LoggerConfiguration()
                            .WriteTo.Logger(logger)
                            .WriteTo.StringBuilder(sb)
                            .CreateLogger();
        
            var exitCode = SilentProcessRunner.ExecuteCommand(
                                                              helmExePath,
                                                              installArgs,
                                                              tempDir.DirectoryPath,
                                                              sprLogger.Debug,
                                                              sprLogger.Information,
                                                              sprLogger.Error,
                                                              CancellationToken.None);

            if (exitCode != 0)
            {
                throw new InvalidOperationException($"Failed to install NFS CSI driver into cluster {clusterName}. Logs: {sb}");
            }
        }

        string BuildNfsCsiDriverInstallArguments()
        {
            return string.Join(" ",
                               "install",
                               "--atomic",
                               "--repo https://raw.githubusercontent.com/kubernetes-csi/csi-driver-nfs/master/charts",
                               "--namespace kube-system",
                               "--version \"v4.*.*\"",
                               $"--kubeconfig \"{KubeConfigPath}\"",
                               "csi-driver-nfs",
                               "csi-driver-nfs"
                              );
        }

        public void Dispose()
        {
            var exitCode = SilentProcessRunner.ExecuteCommand(
                                                              kindExePath,
                                                              //delete the cluster for this test run
                                                              $"delete cluster --name={clusterName}",
                                                              tempDir.DirectoryPath,
                                                              logger.Debug,
                                                              logger.Information,
                                                              logger.Error,
                                                              CancellationToken.None);

            if (exitCode != 0)
            {
                logger.Error("Failed to delete Kind kubernetes cluster {ClusterName}", clusterName);
            }
        }
    }
}
