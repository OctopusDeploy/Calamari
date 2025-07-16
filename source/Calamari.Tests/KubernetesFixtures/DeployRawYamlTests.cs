using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Assent;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.Commands;
using Calamari.Kubernetes.ResourceStatus.Resources;
using Calamari.Testing.Helpers;
using Calamari.Testing.Requirements;
using Calamari.Tests.Helpers;
using Calamari.Tests.KubernetesFixtures.Kind;
using FluentAssertions;
using NUnit.Framework;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using KubernetesSpecialVariables = Calamari.Kubernetes.SpecialVariables;

namespace Calamari.Tests.KubernetesFixtures
{
    [TestFixture]
    [RequiresDotNetCore]
    public class DeployRawYamlTests : CalamariFixture
    {
        const string ResourcePackageFileName = "package.1.0.0.zip";
        const string DeploymentFileName = "customresource.yml";
        const string DeploymentFileName2 = "myapp-deployment.yml";
        const string ServiceFileName = "myapp-service.yml";
        const string ConfigMapFileName = "myapp-configmap1.yml";
        const string ConfigMapFileName2 = "myapp-configmap2.yml";

        readonly ResourceGroupVersionKind simpleDeploymentResourceGvk = new ResourceGroupVersionKind("apps", "v1", "Deployment");
        const string SimpleDeploymentResourceName = "nginx-deployment";

        const string SimpleDeploymentResource =
            "apiVersion: apps/v1\nkind: Deployment\nmetadata:\n  name: nginx-deployment\nspec:\n  selector:\n    matchLabels:\n      app: nginx\n  replicas: 3\n  template:\n    metadata:\n      labels:\n        app: nginx\n    spec:\n      containers:\n      - name: nginx\n        image: nginx:1.14.2\n        ports:\n        - containerPort: 80";

        const string SimpleDeployment2ResourceName = "nginx-deployment";

        const string SimpleDeploymentResource2 =
            "apiVersion: apps/v1\nkind: Deployment\nmetadata:\n  name: nginx-deployment2\nspec:\n  selector:\n    matchLabels:\n      app: nginx2\n  replicas: 1\n  template:\n    metadata:\n      labels:\n        app: nginx2\n    spec:\n      containers:\n      - name: nginx2\n        image: nginx:1.14.2\n        ports:\n        - containerPort: 81\n";

        const string InvalidDeploymentResource =
            "apiVersion: apps/v1\nkind: Deployment\nmetadata:\n  name: nginx-deployment\nspec:\nbad text here\n  selector:\n    matchLabels:\n      app: nginx\n  replicas: 3\n  template:\n    metadata:\n      labels:\n        app: nginx\n    spec:\n      containers:\n      - name: nginx\n        image: nginx:1.14.2\n        ports:\n        - containerPort: 80\n";

        const string FailToDeploymentResource =
            "apiVersion: apps/v1\nkind: Deployment\nmetadata:\n  name: nginx-deployment\nspec:\n  selector:\n    matchLabels:\n      app: nginx\n  replicas: 3\n  template:\n    metadata:\n      labels:\n        app: nginx\n    spec:\n      containers:\n      - name: nginx\n        image: nginx-bad-container-name:1.14.2\n        ports:\n        - containerPort: 80\n";

        const string SimpleServiceResourceName = "nginx-service";

        const string SimpleService =
            "apiVersion: v1\nkind: Service\nmetadata:\n  name: nginx-service\nspec:\n  selector:\n    app.kubernetes.io/name: nginx\n  ports:\n    - protocol: TCP\n      port: 80\n      targetPort: 9376";

        const string SimpleConfigMapResourceName = "game-demo";

        const string SimpleConfigMap =
            "apiVersion: v1\nkind: ConfigMap\nmetadata:\n  name: game-demo\ndata:\n  player_initial_lives: '3'\n  ui_properties_file_name: 'user-interface.properties'\n  game.properties: |\n    enemy.types=aliens,monsters\n    player.maximum-lives=5\n  user-interface.properties: |\n    color.good=purple\n    color.bad=yellow\n    allow.textmode=true";

        const string SimpleConfigMap2ResourceName = "game-demo2";

        const string SimpleConfigMap2 =
            "apiVersion: v1\nkind: ConfigMap\nmetadata:\n  name: game-demo2\ndata:\n  player_initial_lives: '1'\n  ui_properties_file_name: 'user-interface.properties'\n  game.properties: |\n    enemy.types=blobs,foxes\n    player.maximum-lives=10\n  user-interface.properties: |\n    color.good=orange\n    color.bad=pink\n    allow.textmode=false";

        InstallTools installTools;
        TemporaryDirectory tempDir;
        KindClusterInstaller kindClusterInstaller;
        IVariables variables;

        string testNamespace;

        [OneTimeSetUp]
        public async Task SetupInfrastructure()
        {
            installTools = new InstallTools(TestContext.Progress.WriteLine);
            await installTools.Install();
            await installTools.InstallKind();
            await installTools.InstallKubectl();

            tempDir = TemporaryDirectory.Create();

            kindClusterInstaller = new KindClusterInstaller(installTools, tempDir, new InMemoryLog());

            await kindClusterInstaller.InstallCluster();
        }

        [OneTimeTearDown]
        public void TearDownInfrastructure()
        {
            kindClusterInstaller.Dispose();

            tempDir.Dispose();
        }

        public override void SetUpCalamariFixture()
        {
            base.SetUpCalamariFixture();

            variables = new CalamariVariables();

            testNamespace = $"calamari-testing-{Guid.NewGuid():N}";

            //set the namespace for this test
            variables.Set(KubernetesSpecialVariables.Namespace, testNamespace);
            variables.Set(KubernetesSpecialVariables.CustomKubectlExecutable, installTools.KubectlExecutable);

            //Set up the auth information using the same client certificate auth as kind
            var (clusterUrl, certificateAuthorityData, clientCertificateData, clientKeyData) = kindClusterInstaller.GetClusterAuthInfo();

            variables.Set(KubernetesSpecialVariables.ClusterUrl, clusterUrl);

            const string certificateAuthority = "myauthority";
            variables.Set(KubernetesSpecialVariables.CertificateAuthority, certificateAuthority);
            variables.Set(KubernetesSpecialVariables.CertificatePem(certificateAuthority), Encoding.ASCII.GetString(Convert.FromBase64String(certificateAuthorityData)));

            const string clientCert = "myClientCert";
            variables.Set(KubernetesSpecialVariables.ClientCertificate, clientCert);
            variables.Set(KubernetesSpecialVariables.CertificatePem(clientCert), Encoding.ASCII.GetString(Convert.FromBase64String(clientCertificateData)));
            variables.Set(KubernetesSpecialVariables.PrivateKeyPem(clientCert), Encoding.ASCII.GetString(Convert.FromBase64String(clientKeyData)));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void DeployRawYaml_WithRawYamlDeploymentScriptOrCommand_OutputShouldIndicateSuccessfulDeployment(bool usePackage)
        {
            SetupAndRunKubernetesRawYamlDeployment(usePackage, SimpleDeploymentResource);

            var rawLogs = Log.Messages.Select(m => m.FormattedMessage).ToArray();

            AssertObjectStatusMonitoringStarted(rawLogs, (simpleDeploymentResourceGvk, SimpleDeploymentResourceName));

            var objectStatusUpdates = Log.Messages.GetServiceMessagesOfType("k8s-status");

            objectStatusUpdates.Where(m => m.Properties["status"] == "Successful").Should().HaveCount(6);

            rawLogs.Should()
                   .ContainSingle(m =>
                                      m.Contains("Resource status check completed successfully because all resources are deployed successfully"));
        }

        void AssertObjectStatusMonitoringStarted(string[] rawLogs, params (ResourceGroupVersionKind Gvk, string Name)[] resources)
        {
            var resourceStatusCheckLog = "Resource Status Check: 1 new resources have been added:";
            var idx = Array.IndexOf(rawLogs, resourceStatusCheckLog);
            foreach (var (i, gvk, name) in resources.Select((t, i) => (i, t.Gvk, t.Name)))
            {
                rawLogs[idx + i + 1].Should().Be($" - {gvk}/{name} in namespace {testNamespace}");
            }
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void DeployRawYaml_WithInvalidYaml_OutputShouldIndicateFailure(bool usePackage)
        {
            SetupAndRunKubernetesRawYamlDeployment(usePackage, InvalidDeploymentResource, shouldSucceed: false);

            var rawLogs = Log.Messages.Select(m => m.FormattedMessage).Where(m => !m.StartsWith("##octopus") && m != string.Empty).ToArray();

            var fileName = usePackage ? $"deployments{Path.DirectorySeparatorChar}{DeploymentFileName}" : DeploymentFileName;
            var parsingErrorLog =
                $"error: error parsing {fileName}: error converting YAML to JSON: yaml: line 7: could not find expected ':'";
            rawLogs.Should()
                   .ContainSingle(l => l == parsingErrorLog);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void DeployRawYaml_WithYamlThatWillNotSucceed_OutputShouldIndicateFailure(bool usePackage)
        {
            SetupAndRunKubernetesRawYamlDeployment(usePackage, FailToDeploymentResource, shouldSucceed: false);

            var rawLogs = Log.Messages.Select(m => m.FormattedMessage).ToArray();

            AssertObjectStatusMonitoringStarted(rawLogs, (simpleDeploymentResourceGvk, SimpleDeploymentResourceName));

            rawLogs.Should()
                   .ContainSingle(l =>
                                      l == "Resource status check terminated because the timeout has been reached but some resources are still in progress");
        }

        [Test]
        public void DeployRawYaml_WithMultipleYamlFilesGlobPatterns_YamlFilesAppliedInCorrectBatches()
        {
            SetVariablesForKubernetesResourceStatusCheck(30);

            SetVariablesForRawYamlCommand($@"deployments/**/*
                                             services/{ServiceFileName}
                                             configmaps/*.yml");

            string CreatePackageWithMultipleYamlFiles(string directory)
            {
                var packageToPackage = CreatePackageWithFiles(ResourcePackageFileName,
                                                              directory,
                                                              ("deployments", DeploymentFileName, SimpleDeploymentResource),
                                                              (Path.Combine("deployments", "subfolder"), DeploymentFileName2, SimpleDeploymentResource2),
                                                              ("services", ServiceFileName, SimpleService),
                                                              ("services", "EmptyYamlFile.yml", ""),
                                                              ("configmaps", ConfigMapFileName, SimpleConfigMap),
                                                              ("configmaps", ConfigMapFileName2, SimpleConfigMap2),
                                                              (Path.Combine("configmaps", "subfolder"), "InvalidJSONNotUsed.yml", InvalidDeploymentResource));
                return packageToPackage;
            }

            ExecuteCommandAndVerifyResult(KubernetesApplyRawYamlCommand.Name, CreatePackageWithMultipleYamlFiles);

            var rawLogs = Log.Messages.Select(m => m.FormattedMessage).Where(l => !l.StartsWith("##octopus")).ToArray();

            // We take the logs starting from when Calamari starts applying batches
            // to when the last k8s resource is created and compare them in an assent test.
            var startIndex = Array.FindIndex(rawLogs, l => l.StartsWith("Applying Batch #1"));
            var endIndex =
                Array.FindLastIndex(rawLogs, l => l == "Resource Status Check: 2 new resources have been added:") + 2;
            var assentLogs = rawLogs.Skip(startIndex)
                                    .Take(endIndex + 1 - startIndex)
                                    .Where(l => !l.StartsWith("##octopus"))
                                    .ToArray();
            var batch3Index = Array.FindIndex(assentLogs, l => l.StartsWith("Applying Batch #3"));

            // In this case the two config maps have been loaded in reverse order
            // This can happen as Directory.EnumerateFiles() does not behave the
            // same on all platforms.
            // We'll flip them back the right way before performing the Assent Test.
            if (assentLogs[batch3Index + 1].Contains("myapp-configmap1.yml"))
            {
                var configMap1Idx = batch3Index + 1;
                var configMap2Idx = Array.FindIndex(assentLogs, l => l.Contains("myapp-configmap2.yml"));
                var endIdx = Array.FindLastIndex(assentLogs, l => l == "Created Resources:") - 1;
                InPlaceSwap(assentLogs, configMap1Idx, configMap2Idx, endIdx);
            }

            // We need to replace the backslash with forward slash because
            // the slash comes out differently on windows machines.
            var assentString = string.Join("\n", assentLogs).Replace("\\", "/");

            assentString = assentString.Replace(testNamespace, "TEST_NAMESPACE");

            this.Assent(assentString, configuration: AssentConfiguration.DefaultWithPostfix("ApplyingBatches"));

            var resources = new[]
            {
                (Name: SimpleDeploymentResourceName, Label: "Deployment1"),
                (Name: SimpleDeployment2ResourceName, Label: "Deployment2"),
                (Name: SimpleServiceResourceName, Label: "Service1"),
                (Name: SimpleConfigMapResourceName, Label: "ConfigMap1"),
                (Name: SimpleConfigMap2ResourceName, Label: "ConfigMap3")
            };

            var statusMessages = Log.Messages.GetServiceMessagesOfType("k8s-status");

            foreach (var (name, label) in resources)
            {
                // Check that each deployed resource has a "Successful" status reported.
                statusMessages.Should().Contain(m => m.Properties["name"] == name && m.Properties["status"] == "Successful");
            }

            rawLogs.Should()
                   .ContainSingle(m =>
                                      m.Contains("Resource status check completed successfully because all resources are deployed successfully"));
        }

        void SetupAndRunKubernetesRawYamlDeployment(bool usePackage, string resource, bool shouldSucceed = true)
        {
            SetVariablesForKubernetesResourceStatusCheck(shouldSucceed ? 30 : 5);

            SetVariablesForRawYamlCommand("**/*.{yml,yaml}");

            ExecuteCommandAndVerifyResult(KubernetesApplyRawYamlCommand.Name,
                                          usePackage
                                              ? CreateAddPackageFunc(resource)
                                              : CreateAddCustomResourceFileFunc(resource),
                                          shouldSucceed);
        }

        void ExecuteCommandAndVerifyResult(string commandName, Func<string, string> addFilesOrPackageFunc = null, bool shouldSucceed = true)
        {
            using (var dir = TemporaryDirectory.Create())
            {
                var directoryPath = dir.DirectoryPath;
                // Note: the "Test Folder" has a space in it to test that working directories
                // with spaces are handled correctly by Kubernetes Steps.
                var folderPath = Path.Combine(directoryPath, "Test Folder");
                Directory.CreateDirectory(folderPath);

                var packagePath = addFilesOrPackageFunc?.Invoke(folderPath);

                var output = ExecuteCommand(commandName, folderPath, packagePath);

                WriteLogMessagesToTestOutput();

                if (shouldSucceed)
                {
                    output.AssertSuccess();
                }
                else
                {
                    output.AssertFailure();
                }
            }
        }

        CalamariResult ExecuteCommand(string command, string workingDirectory, string packagePath)
        {
            using (var variablesFile = new TemporaryFile(Path.GetTempFileName()))
            {
                variables.Save(variablesFile.FilePath);

                var calamariCommand = Calamari()
                                      .Action(command)
                                      .Argument("variables", variablesFile.FilePath)
                                      .WithWorkingDirectory(workingDirectory)
                                      .OutputToLog(true);

                if (packagePath != null)
                {
                    calamariCommand.Argument("package", packagePath);
                }

                return Invoke(calamariCommand, variables, Log);
            }
        }

        void WriteLogMessagesToTestOutput()
        {
            foreach (var message in Log.Messages)
            {
                Console.WriteLine($"[{message.Level}] {message.FormattedMessage}");
            }
        }

        void SetVariablesForRawYamlCommand(string globPaths)
        {
            variables.Set("Octopus.Action.KubernetesContainers.Namespace", "nginx-2");
            variables.Set(KnownVariables.Package.JsonConfigurationVariablesTargets, "**/*.{yml,yaml}");
            variables.Set(KubernetesSpecialVariables.CustomResourceYamlFileName, globPaths);
        }

        void SetVariablesForKubernetesResourceStatusCheck(int timeout)
        {
            variables.Set("Octopus.Action.Kubernetes.ResourceStatusCheck", "True");
            variables.Set("Octopus.Action.KubernetesContainers.DeploymentWait", "NoWait");
            variables.Set("Octopus.Action.Kubernetes.DeploymentTimeout", timeout.ToString());
            variables.Set("Octopus.Action.Kubernetes.PrintVerboseKubectlOutputOnError", "True");
        }

        Func<string, string> CreateAddCustomResourceFileFunc(string yamlContent)
        {
            return directory =>
                   {
                       CreateResourceYamlFile(directory, DeploymentFileName, yamlContent);
                       if (!variables.IsSet(KubernetesSpecialVariables.CustomResourceYamlFileName))
                       {
                           variables.Set(KubernetesSpecialVariables.CustomResourceYamlFileName, DeploymentFileName);
                       }

                       return null;
                   };
        }

        Func<string, string> CreateAddPackageFunc(string yamlContent)
        {
            return directory =>
                   {
                       var pathInPackage = Path.Combine("deployments", DeploymentFileName);
                       var pathToPackage = CreatePackageWithFiles(ResourcePackageFileName,
                                                                  directory,
                                                                  ("deployments", DeploymentFileName, yamlContent));
                       if (!variables.IsSet(KubernetesSpecialVariables.CustomResourceYamlFileName))
                       {
                           variables.Set(KubernetesSpecialVariables.CustomResourceYamlFileName, pathInPackage);
                       }

                       return pathToPackage;
                   };
        }

        static string CreateResourceYamlFile(string directory, string fileName, string content)
        {
            var pathToCustomResource = Path.Combine(directory, fileName);
            File.WriteAllText(pathToCustomResource, content);
            return pathToCustomResource;
        }

        string CreatePackageWithFiles(string packageFileName,
                                      string currentDirectory,
                                      params (string directory, string fileName, string content)[] files)
        {
            var pathToPackage = Path.Combine(currentDirectory, packageFileName);
            using (var archive = ZipArchive.Create())
            {
                var readStreams = new List<IDisposable>();
                foreach (var (directory, fileName, content) in files)
                {
                    var pathInPackage = Path.Combine(directory, fileName);
                    var readStream = new MemoryStream(Encoding.UTF8.GetBytes(content));
                    readStreams.Add(readStream);
                    archive.AddEntry(pathInPackage, readStream);
                }

                using (var writeStream = File.OpenWrite(pathToPackage))
                {
                    archive.SaveTo(writeStream, CompressionType.Deflate);
                }

                foreach (var readStream in readStreams)
                {
                    readStream.Dispose();
                }
            }

            return pathToPackage;
        }

        void InPlaceSwap(string[] array, int section1StartIndex, int section2StartIndex, int endIndex)
        {
            var length = endIndex + 1 - section1StartIndex;
            var section2TempIndex = section2StartIndex - section1StartIndex;
            var temp = new string[length];
            Array.Copy(array,
                       section1StartIndex,
                       temp,
                       0,
                       length);
            Array.Copy(temp,
                       section2TempIndex,
                       array,
                       section1StartIndex,
                       temp.Length - section2TempIndex);
            Array.Copy(temp,
                       0,
                       array,
                       section1StartIndex + temp.Length - section2TempIndex,
                       section2TempIndex);
        }
    }
}