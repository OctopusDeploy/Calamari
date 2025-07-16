using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Testing.Extensions;
using YamlDotNet.RepresentationModel;

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

            clusterName = $"calamariint-{DateTime.Now:yyyyMMddhhmmss}";
            kubeConfigName = $"{clusterName}.config";
        }

        public async Task InstallCluster()
        {
            var configFilePath = await WriteFileToTemporaryDirectory("kind-config.yaml");
            
            var sw = new Stopwatch();
            sw.Restart();

            var result = SilentProcessRunner.ExecuteCommand(installTools.KindExecutable,
                                                            //we give the cluster a unique name
                                                            $"create cluster --name={clusterName} --kubeconfig=\"{kubeConfigName}\" --config=\"{configFilePath}\"",
                                                            temporaryDirectory.DirectoryPath,
                                                            logger.Info,
                                                            logger.Error);

            sw.Stop();

            if (result.ExitCode != 0)
            {
                logger.ErrorFormat("Failed to create Kind Kubernetes cluster {0}", clusterName);
                throw new InvalidOperationException($"Failed to create Kind Kubernetes cluster {clusterName}");
            }

            logger.InfoFormat("Test cluster kubeconfig path: {0}", KubeConfigPath);

            logger.InfoFormat("Created Kind Kubernetes cluster {0} in {1}", clusterName, sw.Elapsed);

            await SetLocalhostRouting();
        }

        async Task SetLocalhostRouting()
        {
            var filename = CalamariEnvironment.IsRunningOnNix ? "linux-network-routing.yaml" : "docker-desktop-network-routing.yaml";

            var manifestFilePath = await WriteFileToTemporaryDirectory(filename, "manifest.yaml");

            var sb = new StringBuilder();

            var result = SilentProcessRunner.ExecuteCommand(installTools.KubectlExecutable,
                                                            //we give the cluster a unique name
                                                            $"apply -n default -f \"{manifestFilePath}\" --kubeconfig=\"{KubeConfigPath}\"",
                                                            temporaryDirectory.DirectoryPath,
                                                            s =>
                                                            {
                                                                sb.AppendLine(s);
                                                                logger.Info(s);
                                                            },
                                                            s =>
                                                            {
                                                                sb.AppendLine(s);
                                                                logger.Error(s);
                                                            });

            if (result.ExitCode != 0)
            {
                logger.ErrorFormat("Failed to apply localhost routing to cluster {0}", clusterName);
                throw new InvalidOperationException($"Failed to apply localhost routing to cluster {clusterName}. Logs: {sb}");
            }
        }

        async Task<string> WriteFileToTemporaryDirectory(string resourceFileName, string outputFilename = null)
        {
            using (var resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStreamFromPartialName(resourceFileName))
            {
                var filePath = Path.Combine(temporaryDirectory.DirectoryPath, outputFilename ?? resourceFileName);

                using (var file = File.Create(filePath))
                {
                    resourceStream.Seek(0, SeekOrigin.Begin);
                    await resourceStream.CopyToAsync(file);

                    return filePath;
                }
            }
        }

        public void Dispose()
        {
            var result = SilentProcessRunner.ExecuteCommand(
                                                              installTools.KindExecutable,
                                                              //delete the cluster for this test run
                                                              $"delete cluster --name={clusterName}",
                                                              temporaryDirectory.DirectoryPath,
                                                              logger.Info,
                                                              logger.Error);

            if (result.ExitCode != 0)
            {
                logger.ErrorFormat("Failed to delete Kind kubernetes cluster {0}", clusterName);
            }
        }

        public (string clusterUrl, string certificateAuthorityData,  string clientCertificateData, string clientKeyData) GetClusterAuthInfo()
        {
            using (var fileStream = File.OpenRead(KubeConfigPath))
            using (var textReader = new StreamReader(fileStream))
            {
                var yamlStream = new YamlStream();
                yamlStream.Load(textReader);

                var doc = yamlStream.Documents.First();
                var rootNode = (YamlMappingNode)doc.RootNode;

                //Get the cluster url from the cluster node
                var clustersNode = (YamlSequenceNode)rootNode["clusters"];

                //we only have one named cluster node
                var namedClusterNode = (YamlMappingNode)clustersNode.First();

                //get its cluster field
                var clusterNode = (YamlMappingNode)namedClusterNode["cluster"];

                //get the server url
                var serverUrl = ((YamlScalarNode)clusterNode["server"]).Value;
                var certificateAuthorityData = ((YamlScalarNode)clusterNode["certificate-authority-data"]).Value;
                
                
                //Now get the client key data
                var usersNode = (YamlSequenceNode)rootNode["users"];
                
                //we only have one named user
                var namedUserNode = (YamlMappingNode)usersNode.First();
                
                //get its cluster field
                var userNode = (YamlMappingNode)namedUserNode["user"];

                //get the client key data
                var clientCertificateData = ((YamlScalarNode)userNode["client-certificate-data"]).Value;
                var clientKeyData = ((YamlScalarNode)userNode["client-key-data"]).Value;

                return (serverUrl,certificateAuthorityData, clientCertificateData, clientKeyData);
            }
        }
    }
}