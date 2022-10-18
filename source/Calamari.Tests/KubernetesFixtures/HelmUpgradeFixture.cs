using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Calamari.Common.Features.Deployment;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Testing;
using Calamari.Testing.Helpers;
using Calamari.Testing.Requirements;
using Calamari.Tests.Fixtures;
using Calamari.Tests.Helpers;
using Calamari.Util;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Versioning.Semver;

namespace Calamari.Tests.KubernetesFixtures
{
    public abstract class HelmUpgradeFixture : CalamariFixture
    {
        static readonly string ServerUrl = ExternalVariables.Get(ExternalVariable.KubernetesClusterUrl);

        static readonly string ClusterToken = ExternalVariables.Get(ExternalVariable.KubernetesClusterToken);

        ICalamariFileSystem FileSystem { get; set; }
        protected IVariables Variables { get; set; }
        string StagingDirectory { get; set; }
        protected static readonly string ReleaseName = "calamaritest-" + Guid.NewGuid().ToString("N").Substring(0, 6);

        protected static readonly string ConfigMapName = "mychart-configmap-" + ReleaseName;

        protected const string Namespace = "calamari-testing";

        static string HelmOsPlatform => CalamariEnvironment.IsRunningOnWindows ? "windows-amd64" : "linux-amd64";

        HelmVersion? helmVersion;
        TemporaryDirectory explicitVersionTempDirectory;

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            if (ExplicitExeVersion != null)
            {
                await DownloadExplicitHelmExecutable();
                helmVersion = new SemanticVersion(ExplicitExeVersion).Major == 2 ? HelmVersion.V2 : HelmVersion.V3;
            }
            else
            {
                helmVersion = GetVersion();
            }

            async Task DownloadExplicitHelmExecutable()
            {
                explicitVersionTempDirectory = TemporaryDirectory.Create();
                var fileName = Path.GetFullPath(Path.Combine(explicitVersionTempDirectory.DirectoryPath, $"helm-v{ExplicitExeVersion}-{HelmOsPlatform}.tgz"));
                using (new TemporaryFile(fileName))
                {
                    await DownloadHelmPackage(ExplicitExeVersion, fileName);

                    new TarGzipPackageExtractor(ConsoleLog.Instance).Extract(fileName, explicitVersionTempDirectory.DirectoryPath);
                }
            }
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            explicitVersionTempDirectory?.Dispose();
        }

        [SetUp]
        public virtual void SetUp()
        {
            FileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();

            // Ensure staging directory exists and is empty
            StagingDirectory = Path.Combine(Path.GetTempPath(), "CalamariTestStaging");
            FileSystem.EnsureDirectoryExists(StagingDirectory);
            FileSystem.PurgeDirectory(StagingDirectory, FailureOptions.ThrowOnFailure);

            Environment.SetEnvironmentVariable("TentacleJournal",
                Path.Combine(StagingDirectory, "DeploymentJournal.xml"));

            Variables = new VariablesFactory(FileSystem).Create(new CommonOptions("test"));
            Variables.Set(TentacleVariables.Agent.ApplicationDirectoryPath, StagingDirectory);

            //Chart Package
            Variables.Set(PackageVariables.PackageId, "mychart");
            Variables.Set(PackageVariables.PackageVersion, "0.3.7");

            //Helm Options
            Variables.Set(Kubernetes.SpecialVariables.Helm.ReleaseName, ReleaseName);
            Variables.Set(Kubernetes.SpecialVariables.Helm.ClientVersion, helmVersion.ToString());

            //K8S Auth
            Variables.Set(Kubernetes.SpecialVariables.ClusterUrl, ServerUrl);
            Variables.Set(Kubernetes.SpecialVariables.SkipTlsVerification, "True");
            Variables.Set(Kubernetes.SpecialVariables.Namespace, Namespace);
            Variables.Set(SpecialVariables.Account.AccountType, "Token");
            Variables.Set(SpecialVariables.Account.Token, ClusterToken);

            if (ExplicitExeVersion != null)
                Variables.Set(Kubernetes.SpecialVariables.Helm.CustomHelmExecutable, HelmExePath);

            AddPostDeployMessageCheckAndCleanup();
        }

        [Test]
        [RequiresNonFreeBSDPlatform]
        [RequiresNon32BitWindows]
        [RequiresNonMac]
        [Category(TestCategory.PlatformAgnostic)]
        public void NoValues_EmbeddedValuesUsed()
        {
            var result = DeployPackage();

            result.AssertSuccess();

            Assert.AreEqual("Hello Embedded Variables", result.CapturedOutput.OutputVariables["Message"]);
        }

        [Test(Description = "Test the case where the package ID does not match the directory inside the helm archive.")]
        [RequiresNonFreeBSDPlatform]
        [RequiresNon32BitWindows]
        [RequiresNonMac]
        [Category(TestCategory.PlatformAgnostic)]
        public void MismatchPackageIDAndHelmArchivePathWorks()
        {
            var packageName = $"{Variables.Get(PackageVariables.PackageId)}-{Variables.Get(PackageVariables.PackageVersion)}.tgz";
            Variables.Set(PackageVariables.PackageId, "thisisnotamatch");
            Variables.Set(PackageVariables.PackageVersion, "0.3.7");

            var result = DeployPackage(packageName);

            result.AssertSuccess();

            Assert.AreEqual("Hello Embedded Variables", result.CapturedOutput.OutputVariables["Message"]);
        }

        [Test]
        [RequiresNonFreeBSDPlatform]
        [RequiresNon32BitWindows]
        [RequiresNonMac]
        [Category(TestCategory.PlatformAgnostic)]
        public void ExplicitValues_NewValuesUsed()
        {
            //Helm Config
            Variables.Set(Kubernetes.SpecialVariables.Helm.KeyValues, "{\"SpecialMessage\": \"FooBar\"}");

            var result = DeployPackage();
            result.AssertSuccess();
            Assert.AreEqual("Hello FooBar", result.CapturedOutput.OutputVariables["Message"]);
        }

        [Test]
        [RequiresNonFreeBSDPlatform]
        [RequiresNon32BitWindows]
        [RequiresNonMac]
        [Category(TestCategory.PlatformAgnostic)]
        public void ValuesFromPackage_NewValuesUsed()
        {
            //Additional Package
            Variables.Set(PackageVariables.IndexedPackageId("Pack-1"), "CustomValues");
            Variables.Set(PackageVariables.IndexedPackageVersion("Pack-1"), "2.0.0");
            Variables.Set(PackageVariables.IndexedOriginalPath("Pack-1"), GetFixtureResource("Charts", "CustomValues.2.0.0.zip"));
            Variables.Set(Kubernetes.SpecialVariables.Helm.Packages.ValuesFilePath("Pack-1"), "values.yaml");

            //Variable that will replace packaged value in package
            Variables.Set("MySecretMessage", "Variable Replaced In Package");

            var result = DeployPackage();
            result.AssertSuccess();
            Assert.AreEqual("Hello Variable Replaced In Package", result.CapturedOutput.OutputVariables["Message"]);
        }


        [Test]
        [RequiresNonFreeBSDPlatform]
        [RequiresNon32BitWindows]
        [RequiresNonMac]
        [Category(TestCategory.PlatformAgnostic)]
        public void ValuesFromChartPackage_NewValuesUsed()
        {
            //Additional Package
            Variables.Set(Kubernetes.SpecialVariables.Helm.Packages.ValuesFilePath(""), Path.Combine("mychart", "secondary.Development.yaml"));

            var result = DeployPackage();
            result.AssertSuccess();
            Assert.AreEqual("Hello I am in a secondary", result.CapturedOutput.OutputVariables["Message"]);
        }

        [Test]
        [RequiresNonFreeBSDPlatform]
        [RequiresNon32BitWindows]
        [RequiresNonMac]
        [Category(TestCategory.PlatformAgnostic)]
        public void ValuesFromChartPackage_GetSubstituted()
        {
            Variables.Set(PackageVariables.PackageVersion, "0.3.8");
            Variables.Set("SpecialMessage", "octostache is working");

            var result = DeployPackage();
            result.AssertSuccess();
            Assert.AreEqual("Hello octostache is working", result.CapturedOutput.OutputVariables["Message"]);
        }

        [Test]
        [RequiresNonFreeBSDPlatform]
        [RequiresNon32BitWindows]
        [RequiresNonMac]
        [Category(TestCategory.PlatformAgnostic)]
        public void ValuesFromChartPackageWithoutSubDirectory_NewValuesUsed()
        {
            //Additional Package
            Variables.Set(Kubernetes.SpecialVariables.Helm.Packages.ValuesFilePath(""), "secondary.Development.yaml");

            var result = DeployPackage();
            result.AssertSuccess();
            Assert.AreEqual("Hello I am in a secondary", result.CapturedOutput.OutputVariables["Message"]);
        }

        [Test]
        [RequiresNonFreeBSDPlatform]
        [RequiresNon32BitWindows]
        [RequiresNonMac]
        [Category(TestCategory.PlatformAgnostic)]
        public void ValuesFromPackageAndExplicit_ExplicitTakesPrecedence()
        {
            //Helm Config (lets make sure Explicit values take precedence
            Variables.Set(Kubernetes.SpecialVariables.Helm.KeyValues, "{\"SpecialMessage\": \"FooBar\"}");

            //Additional Package
            Variables.Set(PackageVariables.IndexedPackageId("Pack-1"), "CustomValues");
            Variables.Set(PackageVariables.IndexedPackageVersion("Pack-1"), "2.0.0");
            Variables.Set(PackageVariables.IndexedOriginalPath("Pack-1"),
                GetFixtureResource("Charts", "CustomValues.2.0.0.zip"));
            Variables.Set(Kubernetes.SpecialVariables.Helm.Packages.ValuesFilePath("Pack-1"), "values.yaml");

            //Variable that will replace packaged value in package
            Variables.Set("MySecretMessage", "From A Variable Replaced In Package");

            var result = DeployPackage();
            result.AssertSuccess();
            Assert.AreEqual("Hello FooBar", result.CapturedOutput.OutputVariables["Message"]);
        }

        [Test]
        [RequiresNonFreeBSDPlatform]
        [RequiresNon32BitWindows]
        [RequiresNonMac]
        [Category(TestCategory.PlatformAgnostic)]
        public void ValuesFromRawYaml_ValuesAdded()
        {
            Variables.Set(Kubernetes.SpecialVariables.Helm.YamlValues, "\"SpecialMessage\": \"YAML\"");

            var result = DeployPackage();
            result.AssertSuccess();
            Assert.AreEqual("Hello YAML", result.CapturedOutput.OutputVariables["Message"]);
        }

        protected async Task TestCustomHelmExeInPackage_RelativePath(string version)
        {
            var fileName = Path.Combine(Path.GetTempPath(), $"helm-v{version}-{HelmOsPlatform}.tgz");

            using (new TemporaryFile(fileName))
            {
                await DownloadHelmPackage(version, fileName);

                var customHelmExePackageId = Kubernetes.SpecialVariables.Helm.Packages.CustomHelmExePackageKey;
                Variables.Set(PackageVariables.IndexedOriginalPath(customHelmExePackageId), fileName);
                Variables.Set(PackageVariables.IndexedExtract(customHelmExePackageId), "True");
                Variables.Set(PackageVariables.IndexedPackageId(customHelmExePackageId), "helmexe");
                Variables.Set(PackageVariables.IndexedPackageVersion(customHelmExePackageId), version);

                // If package is provided then it should be treated as a relative path
                var customLocation = HelmOsPlatform + Path.DirectorySeparatorChar + "helm";
                Variables.Set(Kubernetes.SpecialVariables.Helm.CustomHelmExecutable, customLocation);

                AddPostDeployMessageCheckAndCleanup();

                var result = DeployPackage();
                result.AssertSuccess();
                result.AssertOutput($"Using custom helm executable at {HelmOsPlatform}\\helm from inside package. Full path at");
            }
        }

        [Test]
        [RequiresNonFreeBSDPlatform]
        [RequiresNon32BitWindows]
        [RequiresNonMac]
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
        [RequiresNonMac]
        [Category(TestCategory.PlatformAgnostic)]
        public void AdditionalArgumentsPassed()
        {
            Variables.Set(Kubernetes.SpecialVariables.Helm.AdditionalArguments, "--dry-run");
            AddPostDeployMessageCheckAndCleanup(explicitNamespace: null, dryRun: true);

            var result = DeployPackage();
            result.AssertSuccess();
            result.AssertOutputMatches("[helm|\\\\helm\"] upgrade (.*) --dry-run");
        }

        protected abstract string ExplicitExeVersion { get; }

        protected string HelmExePath => ExplicitExeVersion == null ? "helm" : Path.Combine(explicitVersionTempDirectory.DirectoryPath, HelmOsPlatform, "helm");

        void AddPostDeployMessageCheckAndCleanup(string explicitNamespace = null, bool dryRun = false)
        {
            if (dryRun)
            {
                // If it's a dry-run we can't fetch the ConfigMap and there's nothing to clean-up
                Variables.Set(KnownVariables.Package.EnabledFeatures, "");
                return;
            }

            var @namespace = explicitNamespace ?? Namespace;

            var kubectlCmd = "kubectl get configmaps " + ConfigMapName + " --namespace " + @namespace +" -o jsonpath=\"{.data.myvalue}\"";
            var syntax = ScriptSyntax.Bash;
            var deleteCommand = DeleteCommand(@namespace, ReleaseName);
            var script = "set_octopusvariable Message \"$("+ kubectlCmd +$")\"\n{HelmExePath} " + deleteCommand;
            if (CalamariEnvironment.IsRunningOnWindows)
            {
                syntax = ScriptSyntax.PowerShell;
                script = $"Set-OctopusVariable -name Message -Value $({kubectlCmd})\r\n{HelmExePath} " + deleteCommand;
            }

            Variables.Set(SpecialVariables.Action.CustomScripts.GetCustomScriptStage(DeploymentStages.PostDeploy, syntax), script);
            Variables.Set(KnownVariables.Package.EnabledFeatures, KnownVariables.Features.CustomScripts);
        }

        string DeleteCommand(string @namespace, string releaseName)
        {
            switch (helmVersion)
            {
                case HelmVersion.V2:
                    return $"delete {releaseName} --purge";
                case HelmVersion.V3:
                    return $"uninstall {releaseName} --namespace {@namespace}";
                default:
                    throw new ArgumentOutOfRangeException(nameof(helmVersion), helmVersion, "Unrecognized Helm version");
            }
        }

        protected CalamariResult DeployPackage(string packageName = null)
        {
            using (var variablesFile = new TemporaryFile(Path.GetTempFileName()))
            {
                if (packageName == null)
                {
                    packageName = $"{Variables.Get(PackageVariables.PackageId)}-{Variables.Get(PackageVariables.PackageVersion)}.tgz";
                }
                var pkg = GetFixtureResource("Charts", packageName);
                Variables.Save(variablesFile.FilePath);

                return InvokeInProcess(Calamari()
                    .Action("helm-upgrade")
                    .Argument("package", pkg)
                    .Argument("variables", variablesFile.FilePath));
            }
        }

        static async Task DownloadHelmPackage(string version, string fileName)
        {
            using (var client = new HttpClient())
            {
                using (var fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var stream = await client.GetStreamAsync($"https://get.helm.sh/helm-v{version}-{HelmOsPlatform}.tar.gz"))
                {
                    await stream.CopyToAsync(fileStream);
                }
            }
        }

        static HelmVersion GetVersion()
        {
            StringBuilder stdout = new StringBuilder();
            var result = SilentProcessRunner.ExecuteCommand("helm", "version --client --short", Environment.CurrentDirectory, output => stdout.AppendLine(output), error => { });

            result.ExitCode.Should().Be(0, $"Failed to retrieve version from Helm (Exit code {result.ExitCode}). Error output: \r\n{result.ErrorOutput}");

            return ParseVersion(stdout.ToString());
        }

        //versionString from "helm version --client --short"
        static HelmVersion ParseVersion(string versionString)
        {
            //eg of output for helm 2: Client: v2.16.1+gbbdfe5e
            //eg of output for helm 3: v3.0.1+g7c22ef9

            var indexOfVersionIdentifier = versionString.IndexOf('v');
            if (indexOfVersionIdentifier == -1)
                throw new FormatException($"Failed to find version identifier from '{versionString}'.");

            var indexOfVersionNumber = indexOfVersionIdentifier + 1;
            if (indexOfVersionNumber >= versionString.Length)
                throw new FormatException($"Failed to find version number from '{versionString}'.");

            var version = versionString[indexOfVersionNumber];
            switch (version)
            {
                case '3':
                    return HelmVersion.V3;
                case '2':
                    return HelmVersion.V2;
                default:
                    throw new InvalidOperationException($"Unsupported helm version '{version}'");
            }
        }
    }
}
