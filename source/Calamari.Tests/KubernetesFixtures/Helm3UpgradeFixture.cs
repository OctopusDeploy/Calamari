using System;
using System.IO;
using System.Linq;
using System.Net;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;
using Calamari.Tests.Helpers;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Calamari.Kubernetes;
using Calamari.Tests.Fixtures;
using NUnit.Framework;
using Octostache;
using SharpCompress.Archives.GZip;
using SpecialVariables = Calamari.Deployment.SpecialVariables;

namespace Calamari.Tests.KubernetesFixtures
{
    [TestFixture]
    public class Helm3UpgradeFixture : CalamariFixture
    {
        static readonly string ServerUrl = ExternalVariables.Get(ExternalVariable.KubernetesClusterUrl);
        static readonly string ClusterToken = ExternalVariables.Get(ExternalVariable.KubernetesClusterToken);
        static readonly string ReleaseName = "calamaritest-" + Guid.NewGuid().ToString("N").Substring(0, 6);
        static readonly string ConfigMapName = "mychart-configmap-" + ReleaseName;
        const string Namespace = "calamari-testing";
        const string ChartPackageName = "mychart-0.3.7.tgz";

        TemporaryDirectory helm3ExecutableDirectory;
        ICalamariFileSystem FileSystem { get; set; }
        VariableDictionary Variables { get; set; }
        string StagingDirectory { get; set; }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            DownloadHelm3Executable();
            
            void DownloadHelm3Executable()
            {
                const string version = "3.0.2";
                var platformFile = CalamariEnvironment.IsRunningOnWindows ? "windows-amd64" : "linux-amd64";
                var tempPath = Path.GetTempPath();
                var fileName = Path.GetFullPath(Path.Combine(tempPath, $"helm-v{version}-{platformFile}.tgz"));
                using (new TemporaryFile(fileName))
                {
                    using (var myWebClient = new WebClient())
                    {
                        myWebClient.DownloadFile($"https://get.helm.sh/helm-v{version}-{platformFile}.tar.gz", fileName);
                    }

                    new TarGzipPackageExtractor().Extract(fileName, tempPath, false);
                    
                    helm3ExecutableDirectory = new TemporaryDirectory(Path.Combine(tempPath, platformFile));
                }
            }
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            helm3ExecutableDirectory?.Dispose();
        }

        [SetUp]
        public void SetUp()
        {
            FileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();

            // Ensure staging directory exists and is empty
            StagingDirectory = Path.Combine(Path.GetTempPath(), "CalamariTestStaging");
            FileSystem.EnsureDirectoryExists(StagingDirectory);
            FileSystem.PurgeDirectory(StagingDirectory, FailureOptions.ThrowOnFailure);

            Environment.SetEnvironmentVariable("TentacleJournal",
                Path.Combine(StagingDirectory, "DeploymentJournal.xml"));
            Variables = new VariableDictionary();
            Variables.EnrichWithEnvironmentVariables();
            Variables.Set(SpecialVariables.Tentacle.Agent.ApplicationDirectoryPath, StagingDirectory);

            // Chart Package
            Variables.Set(SpecialVariables.Package.NuGetPackageId, "mychart");
            Variables.Set(SpecialVariables.Package.NuGetPackageVersion, "0.3.7");
            Variables.Set(SpecialVariables.Packages.PackageId(""), $"#{{{SpecialVariables.Package.NuGetPackageId}}}");
            Variables.Set(SpecialVariables.Packages.PackageVersion(""), $"#{{{SpecialVariables.Package.NuGetPackageVersion}}}");

            // Helm Options
            Variables.Set(Kubernetes.SpecialVariables.Helm.ReleaseName, ReleaseName);

            // K8S Auth
            Variables.Set(Kubernetes.SpecialVariables.ClusterUrl, ServerUrl);
            Variables.Set(Kubernetes.SpecialVariables.SkipTlsVerification, "True");
            Variables.Set(Kubernetes.SpecialVariables.Namespace, Namespace);
            Variables.Set(SpecialVariables.Account.AccountType, "Token");
            Variables.Set(SpecialVariables.Account.Token, ClusterToken);
            Variables.Set(Kubernetes.SpecialVariables.Helm.Namespace, Namespace);

            AddPostDeployMessageCheckAndCleanup();
        }

        [Test]
        [RequiresNonFreeBSDPlatform]
        [RequiresNon32BitWindows]
        [RequiresNonMacAttribute]
        [Category(TestCategory.PlatformAgnostic)]
        public void Upgrade_Succeeds()
        {
            var result = DeployPackage();

            result.AssertSuccess();
            result.AssertOutputMatchesCaseInsensitive($"NAMESPACE: {Namespace}");
            result.AssertOutputMatchesCaseInsensitive("STATUS: DEPLOYED");
            result.AssertOutputMatchesCaseInsensitive($"release \"{ReleaseName}\" uninstalled");
//            result.AssertNoOutput("Using custom helm executable at");

            Assert.AreEqual(ReleaseName.ToLower(), result.CapturedOutput.OutputVariables["ReleaseName"]);
        }

        [Test]
        [RequiresNonFreeBSDPlatform]
        [RequiresNon32BitWindows]
        [RequiresNonMacAttribute]
        [Category(TestCategory.PlatformAgnostic)]
        public void NoValues_EmbeddedValuesUsed()
        {
            var result = DeployPackage();
            result.AssertSuccess();
            Assert.AreEqual("Hello Embedded Variables", result.CapturedOutput.OutputVariables["Message"]);
        }

        [Test]
        [RequiresNonFreeBSDPlatform]
        [RequiresNon32BitWindows]
        [RequiresNonMacAttribute]
        [Category(TestCategory.PlatformAgnostic)]
        public void ExplicitValues_NewValuesUsed()
        {
            // Helm Config
            Variables.Set(Kubernetes.SpecialVariables.Helm.KeyValues, "{\"SpecialMessage\": \"FooBar\"}");

            var result = DeployPackage();
            result.AssertSuccess();
            Assert.AreEqual("Hello FooBar", result.CapturedOutput.OutputVariables["Message"]);
        }

        [Test]
        [RequiresNonFreeBSDPlatform]
        [RequiresNon32BitWindows]
        [RequiresNonMacAttribute]
        [Category(TestCategory.PlatformAgnostic)]
        public void ValuesFromPackage_NewValuesUsed()
        {
            // Additional Package
            Variables.Set(SpecialVariables.Packages.PackageId("Pack-1"), "CustomValues");
            Variables.Set(SpecialVariables.Packages.PackageVersion("Pack-1"), "2.0.0");
            Variables.Set(SpecialVariables.Packages.OriginalPath("Pack-1"), GetFixtureResouce("Charts", "CustomValues.2.0.0.zip"));
            Variables.Set(Kubernetes.SpecialVariables.Helm.Packages.ValuesFilePath("Pack-1"), "values.yaml");

            // Variable that will replace packaged value in package
            Variables.Set("MySecretMessage", "Variable Replaced In Package");

            var result = DeployPackage();
            result.AssertSuccess();
            Assert.AreEqual("Hello Variable Replaced In Package", result.CapturedOutput.OutputVariables["Message"]);
        }

        [Test]
        [RequiresNonFreeBSDPlatform]
        [RequiresNon32BitWindows]
        [RequiresNonMacAttribute]
        [Category(TestCategory.PlatformAgnostic)]
        public void ValuesFromChartPackage_NewValuesUsed()
        {
            // Additional Package
            Variables.Set(Kubernetes.SpecialVariables.Helm.Packages.ValuesFilePath(""), Path.Combine("mychart", "secondary.Development.yaml"));

            var result = DeployPackage();
            result.AssertSuccess();
            Assert.AreEqual("Hello I am in a secondary", result.CapturedOutput.OutputVariables["Message"]);
        }

        [Test]
        [RequiresNonFreeBSDPlatform]
        [RequiresNon32BitWindows]
        [RequiresNonMacAttribute]
        [Category(TestCategory.PlatformAgnostic)]
        public void ValuesFromChartPackageWithoutSubDirectory_NewValuesUsed()
        {
            // Additional Package
            Variables.Set(Kubernetes.SpecialVariables.Helm.Packages.ValuesFilePath(""), "secondary.Development.yaml");

            var result = DeployPackage();
            result.AssertSuccess();
            Assert.AreEqual("Hello I am in a secondary", result.CapturedOutput.OutputVariables["Message"]);
        }

        [Test]
        [RequiresNonFreeBSDPlatform]
        [RequiresNon32BitWindows]
        [RequiresNonMacAttribute]
        [Category(TestCategory.PlatformAgnostic)]
        public void ValuesFromPackageAndExplicit_ExplicitTakesPrecedence()
        {
            // Helm Config (lets make sure Explicit values take precedence
            Variables.Set(Kubernetes.SpecialVariables.Helm.KeyValues, "{\"SpecialMessage\": \"FooBar\"}");

            // Additional Package
            Variables.Set(SpecialVariables.Packages.PackageId("Pack-1"), "CustomValues");
            Variables.Set(SpecialVariables.Packages.PackageVersion("Pack-1"), "2.0.0");
            Variables.Set(SpecialVariables.Packages.OriginalPath("Pack-1"),
                GetFixtureResouce("Charts", "CustomValues.2.0.0.zip"));
            Variables.Set(Kubernetes.SpecialVariables.Helm.Packages.ValuesFilePath("Pack-1"), "values.yaml");

            // Variable that will replace packaged value in package
            Variables.Set("MySecretMessage", "From A Variable Replaced In Package");

            var result = DeployPackage();
            result.AssertSuccess();
            Assert.AreEqual("Hello FooBar", result.CapturedOutput.OutputVariables["Message"]);
        }

        [Test]
        [RequiresNonFreeBSDPlatform]
        [RequiresNon32BitWindows]
        [RequiresNonMacAttribute]
        [Category(TestCategory.PlatformAgnostic)]
        public void ValuesFromRawYaml_ValuesAdded()
        {
            Variables.Set(Kubernetes.SpecialVariables.Helm.YamlValues, "\"SpecialMessage\": \"YAML\"");

            var result = DeployPackage();
            result.AssertSuccess();
            Assert.AreEqual("Hello YAML", result.CapturedOutput.OutputVariables["Message"]);
        }

        [Test]
        [RequiresNonFreeBSDPlatform]
        [RequiresNon32BitWindows]
        [RequiresNonMacAttribute]
        [Category(TestCategory.PlatformAgnostic)]
        public void CustomDownloadedHelmExe_RelativePath()
        {
            const string version = "3.0.2";
            var platformFile = CalamariEnvironment.IsRunningOnWindows ? "windows-amd64" : "linux-amd64";
            var fileName = Path.Combine(Path.GetTempPath(), $"helm-v{version}-{platformFile}.tgz");
            using (new TemporaryFile(fileName))
            {
                using (var myWebClient = new WebClient())
                {
                    myWebClient.DownloadFile($"https://get.helm.sh/helm-v{version}-{platformFile}.tar.gz", fileName);
                }

                var customHelmExePackageId = Kubernetes.SpecialVariables.Helm.Packages.CustomHelmExePackageKey;
                Variables.Set(SpecialVariables.Packages.OriginalPath(customHelmExePackageId), fileName);
                Variables.Set(SpecialVariables.Packages.Extract(customHelmExePackageId), "True");
                Variables.Set(SpecialVariables.Packages.PackageId(customHelmExePackageId), "helmexe");
                Variables.Set(SpecialVariables.Packages.PackageVersion(customHelmExePackageId), version);

                // If package is provided then it should be treated as a relative path
                var customLocation = platformFile + Path.DirectorySeparatorChar + "helm";
                Variables.Set(Kubernetes.SpecialVariables.Helm.CustomHelmExecutable, customLocation);

                AddPostDeployMessageCheckAndCleanup(null, false, true);

                var result = DeployPackage(customLocation);
                result.AssertSuccess();
                result.AssertOutput("Using custom helm executable at");
            }
        }

        [Test]
        [RequiresNonFreeBSDPlatform]
        [RequiresNon32BitWindows]
        [RequiresNonMacAttribute]
        [Category(TestCategory.PlatformAgnostic)]
        public void Namespace_Override_Used()
        {
            const string @namespace = "calamari-testing-foo";
            Variables.Set(Kubernetes.SpecialVariables.Helm.Namespace, @namespace);
            AddPostDeployMessageCheckAndCleanup(@namespace);

            var result = DeployPackage();
            result.AssertSuccess();
            Assert.AreEqual("Hello Embedded Variables", result.CapturedOutput.OutputVariables["Message"]);
        }

        [Test]
        [RequiresNonFreeBSDPlatform]
        [RequiresNon32BitWindows]
        [RequiresNonMacAttribute]
        [Category(TestCategory.PlatformAgnostic)]
        public void AdditionalArgumentsPassed()
        {
            Variables.Set(Kubernetes.SpecialVariables.Helm.AdditionalArguments, "--dry-run");
            AddPostDeployMessageCheckAndCleanup(explicitNamespace: null, dryRun: true);

            var result = DeployPackage();
            result.AssertSuccess();
            result.AssertOutputMatches("helm(\") upgrade (.*) --dry-run");
        }

        void AddPostDeployMessageCheckAndCleanup(string explicitNamespace = null, bool dryRun = false, bool isUsingCustomVersion3Package = false)
        {
            if (dryRun)
            {
                // If it's a dry-run we can't fetch the ConfigMap and there's nothing to clean-up
                Variables.Set(SpecialVariables.Package.EnabledFeatures, "");
                return;
            }

            var @namespace = explicitNamespace ?? Namespace;

            if (!isUsingCustomVersion3Package)
                Variables.Set(Kubernetes.SpecialVariables.Helm.CustomHelmExecutable, Path.Combine(helm3ExecutableDirectory.DirectoryPath, "helm"));

            var tempDirectory = FileSystem.CreateTemporaryDirectory();
            var helmDeleteCommand = isUsingCustomVersion3Package ? new Helm3CommandBuilder() : HelmBuilder.GetHelmCommandBuilderForInstalledHelmVersion(Variables, tempDirectory);
            helmDeleteCommand
                .SetExecutable(Variables)
                .WithCommand("uninstall")
                .Purge()
                .Namespace(@namespace)
                .AdditionalArguments($"\"{ReleaseName}\"");

            var kubectlCmd = "kubectl get configmaps " + ConfigMapName + " --namespace " + @namespace + " -o jsonpath=\"{.data.myvalue}\"";
            var syntax = ScriptSyntax.Bash;
            var script = $"set_octopusvariable Message \"$({kubectlCmd})\"\n{helmDeleteCommand.Build()}";
            if (CalamariEnvironment.IsRunningOnWindows)
            {
                syntax = ScriptSyntax.PowerShell;
                script = $"Set-OctopusVariable -name Message -Value $({kubectlCmd})\r\n{helmDeleteCommand.Build()}";
            }

            Variables.Set(SpecialVariables.Action.CustomScripts.GetCustomScriptStage(DeploymentStages.PostDeploy, syntax), script);
            Variables.Set(SpecialVariables.Package.EnabledFeatures, SpecialVariables.Features.CustomScripts);
        }

        CalamariResult DeployPackage(string customHelmExe = null)
        {
            if (customHelmExe == null)
                Variables.Set(Kubernetes.SpecialVariables.Helm.CustomHelmExecutable, Path.Combine(helm3ExecutableDirectory.DirectoryPath, "helm"));
            
            using (var variablesFile = new TemporaryFile(Path.GetTempFileName()))
            {
                var pkg = GetFixtureResouce("Charts", ChartPackageName);
                Variables.Save(variablesFile.FilePath);

                return InvokeInProcess(Calamari()
                    .Action("helm-upgrade")
                    .Argument("package", pkg)
                    .Argument("variables", variablesFile.FilePath));
            }
        }
    }
}