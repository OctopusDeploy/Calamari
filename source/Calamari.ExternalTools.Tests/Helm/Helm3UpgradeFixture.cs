using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Features.Deployment;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.ExternalTools.Tests.Infrastructure;
using Calamari.ExternalTools.Tests.Infrastructure.ToolStrategies;
using Calamari.Testing;
using Calamari.Testing.Helpers;
using Calamari.Testing.Requirements;
using NUnit.Framework;

namespace Calamari.ExternalTools.Tests.Helm
{
    [TestFixture]
    public class Helm3UpgradeFixture : ExternalToolFixture
    {
        protected override string PrimaryToolName => "helm";

        protected override Task<string> DownloadTool(string destinationDir, string version, HttpClient client)
            => HelmStrategy.Download(destinationDir, version, client);

        static readonly string ReleaseName = "calamaritest-" + Guid.NewGuid().ToString("N").Substring(0, 6);
        static readonly string ConfigMapName = "mychart-configmap-" + ReleaseName;
        const string Namespace = "calamari-testing";

        static string ServerUrl;
        static string ClusterToken;

        ICalamariFileSystem FileSystem { get; set; }
        IVariables Variables { get; set; }
        string StagingDirectory { get; set; }
        new InMemoryLog Log { get; set; }

        static readonly CancellationTokenSource CancellationTokenSource = new();
        readonly CancellationToken cancellationToken = CancellationTokenSource.Token;

        [OneTimeSetUp]
        public async Task FetchClusterCredentials()
        {
            ServerUrl = await ExternalVariables.Get(ExternalVariable.KubernetesClusterUrl, cancellationToken);
            ClusterToken = await ExternalVariables.Get(ExternalVariable.KubernetesClusterToken, cancellationToken);
        }

        [SetUp]
        public void SetUp()
        {
            Log = new InMemoryLog();
            FileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();

            StagingDirectory = Path.Combine(Path.GetTempPath(), "CalamariTestStaging");
            FileSystem.EnsureDirectoryExists(StagingDirectory);
            FileSystem.PurgeDirectory(StagingDirectory, FailureOptions.ThrowOnFailure);

            var packageExtractionDirectory = Path.Combine(Environment.CurrentDirectory, ExtractPackage.StagingDirectoryName);
            FileSystem.PurgeDirectory(packageExtractionDirectory, FailureOptions.ThrowOnFailure);

            Environment.SetEnvironmentVariable("TentacleJournal",
                Path.Combine(StagingDirectory, "DeploymentJournal.xml"));

            Variables = new CalamariVariables();
            Variables.Set(TentacleVariables.Agent.ApplicationDirectoryPath, StagingDirectory);

            // Chart package
            Variables.Set(PackageVariables.PackageId, "mychart");
            Variables.Set(PackageVariables.PackageVersion, "0.3.7");

            // Helm options
            Variables.Set(Kubernetes.SpecialVariables.Helm.ReleaseName, ReleaseName);
            Variables.Set(Kubernetes.SpecialVariables.Helm.CustomHelmExecutable, ToolExecutablePath);

            // K8S auth
            Variables.Set(Kubernetes.SpecialVariables.ClusterUrl, ServerUrl);
            Variables.Set(Kubernetes.SpecialVariables.SkipTlsVerification, "True");
            Variables.Set(Kubernetes.SpecialVariables.Namespace, Namespace);
            Variables.Set(SpecialVariables.Account.AccountType, "Token");
            Variables.Set(SpecialVariables.Account.Token, ClusterToken);

            AddPostDeployMessageCheckAndCleanup();
        }

        [Test]
        [RequiresNonFreeBSDPlatform]
        [RequiresNon32BitWindows]
        [Category(TestCategory.PlatformAgnostic)]
        public void Upgrade_Succeeds()
        {
            var result = DeployPackage();

            result.AssertSuccess();
            result.AssertOutputMatches($"NAMESPACE: {Namespace}");
            result.AssertOutputMatches("STATUS: deployed");
            result.AssertOutputMatches($"release \"{ReleaseName}\" uninstalled");
            result.AssertOutput("Using custom helm executable at " + ToolExecutablePath);

            Assert.AreEqual(ReleaseName.ToLower(), result.CapturedOutput.OutputVariables["ReleaseName"]);
        }

        // --- Helpers ---

        string GetChartPath(params string[] paths)
            => Path.Combine(TestEnvironment.CurrentWorkingDirectory, "Helm", "Charts", Path.Combine(paths));

        void AddPostDeployMessageCheckAndCleanup(string explicitNamespace = null, bool dryRun = false)
        {
            if (dryRun)
            {
                Variables.Set(KnownVariables.Package.EnabledFeatures, "");
                return;
            }

            var @namespace = explicitNamespace ?? Namespace;
            var kubectlCmd = "kubectl get configmaps " + ConfigMapName + " --namespace " + @namespace + " -o jsonpath=\"{.data.myvalue}\"";
            var deleteCommand = $"uninstall {ReleaseName} --namespace {@namespace}";

            if (CalamariEnvironment.IsRunningOnWindows)
            {
                var script = $"Set-OctopusVariable -name Message -Value $({kubectlCmd})\r\n{ToolExecutablePath} {deleteCommand}";
                Variables.Set(SpecialVariables.Action.CustomScripts.GetCustomScriptStage(DeploymentStages.PostDeploy, ScriptSyntax.PowerShell), script);
            }
            else
            {
                var script = "set_octopusvariable Message \"$(" + kubectlCmd + $")\"\n{ToolExecutablePath} " + deleteCommand;
                Variables.Set(SpecialVariables.Action.CustomScripts.GetCustomScriptStage(DeploymentStages.PostDeploy, ScriptSyntax.Bash), script);
            }

            Variables.Set(KnownVariables.Package.EnabledFeatures, KnownVariables.Features.CustomScripts);
        }

        CalamariResult DeployPackage(string packageName = null)
        {
            using var variablesFile = new TemporaryFile(Path.GetTempFileName());

            if (packageName == null)
                packageName = $"{Variables.Get(PackageVariables.PackageId)}-{Variables.Get(PackageVariables.PackageVersion)}.tgz";

            var pkg = GetChartPath(packageName);
            var encryptionKey = Variables.SaveAsEncryptedExecutionVariables(variablesFile.FilePath);

            var command = CalamariCommandHelper.CreateCommand()
                .Action("helm-upgrade")
                .Argument("package", pkg)
                .VariablesFileArguments(variablesFile.FilePath, encryptionKey);

            return CalamariCommandHelper.InvokeInProcess(command, Log);
        }
    }
}
