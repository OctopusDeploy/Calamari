#if NETCORE
#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Kubernetes;
using Octostache;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace Calamari.Tests.KubernetesFixtures.Tools
{

    public static class StringBuilderLogEventSinkExtensions
    {
        public static LoggerConfiguration StringBuilder(this LoggerSinkConfiguration configuration, StringBuilder stringBuilder, IFormatProvider? formatProvider = null)
            => configuration.Sink(new StringBuilderLogEventSink(stringBuilder, formatProvider));
    }

    public class StringBuilderLogEventSink : ILogEventSink
    {
        readonly StringBuilder stringBuilder;
        readonly IFormatProvider? formatProvider;

        public StringBuilderLogEventSink(StringBuilder stringBuilder, IFormatProvider? formatProvider)
        {
            this.stringBuilder = stringBuilder;
            this.formatProvider = formatProvider;
        }

        public void Emit(LogEvent logEvent)
        {
            var message = logEvent.RenderMessage(formatProvider);
            stringBuilder.AppendLine(message);
        }
    }

    public class KubernetesClusterInstaller
    {
        readonly string clusterName;
        readonly string kubeConfigName;

        readonly TemporaryDirectory tempDir;
        readonly string kindExePath;
        //readonly string helmExePath;
        readonly string kubeCtlPath;
        readonly ILogger logger;

        public string KubeConfigPath => Path.Combine(tempDir.DirectoryPath, kubeConfigName);
        public string ClusterName => clusterName;

        public KubernetesClusterInstaller(TemporaryDirectory tempDirectory,
                                          string kindExePath,
                                          string helmExePath,
                                          string kubeCtlPath,
                                          ILogger logger)
        {
            tempDir = tempDirectory;
            this.kindExePath = kindExePath;
           // this.helmExePath = helmExePath;
            this.kubeCtlPath = kubeCtlPath;
            this.logger = logger;

            clusterName = $"tentacleint-{DateTime.Now:yyyyMMddhhmmss}";
            kubeConfigName = $"{clusterName}.config";
        }

        public async Task Install()
        {
            var configFilePath = await WriteFileToTemporaryDirectory("kind-config.yml");

            var sw = new Stopwatch();
            sw.Restart();
            var exitCode = SilentProcessRunner.ExecuteCommand(
                                                              kindExePath,
                                                              //we give the cluster a unique name
                                                              $"create cluster --name={clusterName} --config=\"{configFilePath}\" --kubeconfig=\"{kubeConfigName}\"",
                                                              tempDir.DirectoryPath,
                                                              new Dictionary<string, string>(),
                                                              (m) => logger.Debug(m),
                                                              (m) => logger.Error(m));

            sw.Stop();

            if (exitCode.ExitCode != 0)
            {
                logger.Error("Failed to create Kind Kubernetes cluster {ClusterName}", clusterName);
                throw new InvalidOperationException($"Failed to create Kind Kubernetes cluster {clusterName}");
            }

            logger.Information("Test cluster kubeconfig path: {Path:l}", KubeConfigPath);

            logger.Information("Created Kind Kubernetes cluster {ClusterName} in {ElapsedTime}", clusterName, sw.Elapsed);

            await SetLocalhostRouting();

            ExtractLoginDetails();
            //await InstallNfsCsiDriver();
        }

        public (ClusterEndpoint ClusterEndpoint, ClusterUser ClusterUser) ExtractLoginDetails()
        {
            var config = Path.Combine(tempDir.DirectoryPath,$"{clusterName}.config");
            
            var deserializer = new DeserializerBuilder().Build();
            var data = deserializer.Deserialize<dynamic>(File.ReadAllText(config));
            
            var cluster = data["clusters"][0]["cluster"];
            var clusterCert = cluster["certificate-authority-data"].ToString();
            var clusterUrl = cluster["server"].ToString();
            
            var user = data["users"][0]["user"];
            var clientCertPem = user["client-certificate-data"].ToString();
            var clientCertKey = user["client-key-data"].ToString();

            return (new ClusterEndpoint(clusterUrl, clusterCert), new ClusterUser(clientCertPem, clientCertKey));
        }
        
        async Task SetLocalhostRouting()
        {
            var filename = PlatformDetection.IsRunningOnNix ? "linux-network-routing.yml" : "docker-desktop-network-routing.yml";

            var manifestFilePath = await WriteFileToTemporaryDirectory(filename, "manifest.yml");

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
                                                              new Dictionary<string, string>(),
                                                              (m) => logger.Debug(m),
                                                              (m) => logger.Information(m));

            if (exitCode.ExitCode != 0)
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

        public void Dispose()
        {
            var exitCode = SilentProcessRunner.ExecuteCommand(
                                                              kindExePath,
                                                              //delete the cluster for this test run
                                                              $"delete cluster --name={clusterName}",
                                                              tempDir.DirectoryPath,
                                                              new Dictionary<string, string>(),
                                                              (m) => logger.Debug(m),
                                                              (m) => logger.Information(m));

            
            if (exitCode.ExitCode != 0)
            {
                logger.Error("Failed to delete Kind kubernetes cluster {ClusterName}", clusterName);
            }
        }
    }
    
    public static class AssemblyExtensions
    {
        public static Stream GetManifestResourceStreamFromPartialName(this Assembly assembly, string filename)
        {
            var manifests = assembly.GetManifestResourceNames();
            var valuesFileName = manifests.Single(n => n.Contains(filename, StringComparison.OrdinalIgnoreCase));
            return assembly.GetManifestResourceStream(valuesFileName)!;
        }
    }
}

public struct ClusterUser
{
    public ClusterUser(string clientCertPem, string clientCertKey)
    {
        ClientCertPem = clientCertPem;
        ClientCertKey = clientCertKey;
    }

    public string ClientCertPem { get;  }
    public string ClientCertKey { get;  }
}
public struct ClusterEndpoint
{
    public ClusterEndpoint(string clusterUrl, string clusterCert)
    {
        ClusterCert = clusterCert;
        ClusterUrl = clusterUrl;
    }

    public string ClusterCert { get; }
    public string ClusterUrl { get; }
}

#endif