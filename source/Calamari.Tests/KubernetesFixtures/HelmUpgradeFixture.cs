using System;
using System.IO;
using System.Net;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Tests.Helpers;
using  Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Calamari.Tests.Fixtures;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.KubernetesFixtures
{
    [TestFixture]
    public class HelmUpgradeFixture : CalamariFixture
    {
        private static readonly string ServerUrl = ExternalVariables.Get(ExternalVariable.KubernetesClusterUrl);
        private static readonly string ClusterToken = ExternalVariables.Get(ExternalVariable.KubernetesClusterToken);

        private ICalamariFileSystem FileSystem { get; set; }
        private VariableDictionary Variables { get; set; }
        private string StagingDirectory { get; set; }
        private static readonly string ReleaseName = "calamaritest-" + Guid.NewGuid().ToString("N").Substring(0, 6);
        
        private static readonly string ConfigMapName = "mychart-configmap-" + ReleaseName;

        private const string Namespace = "calamari-testing";
        private const string ChartPackageName = "mychart-0.3.7.tgz";


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

            //Chart Pckage
            Variables.Set(SpecialVariables.Package.NuGetPackageId, "mychart");
            Variables.Set(SpecialVariables.Package.NuGetPackageVersion, "0.3.7");

            //Helm Options
            Variables.Set(Kubernetes.SpecialVariables.Helm.ReleaseName, ReleaseName);

            //K8S Auth
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
        [RequiresNonMacAttribute]
        [Category(TestCategory.PlatformAgnostic)]
        public void Upgrade_Succeeds()
        {
            var result = DeployPackage();

            //res.AssertOutputMatches("NAME:   mynewrelease"); //Does not appear on upgrades, only installs
            result.AssertSuccess();
            result.AssertOutputMatches($"NAMESPACE: {Namespace}");
            result.AssertOutputMatches("STATUS: DEPLOYED");
            result.AssertOutputMatches(ConfigMapName);
            result.AssertOutputMatches($"release \"{ReleaseName}\" deleted");
            result.AssertNoOutput("Using custom helm executable at");
            
            Assert.AreEqual(ReleaseName.ToLower(), result.CapturedOutput.OutputVariables["ReleaseName"]);
        }

        [Test]
        [RequiresNonFreeBSDPlatform]
        [RequiresNon32BitWindows]
        [RequiresNonMacAttribute]
        [Category(TestCategory.PlatformAgnostic)]
        public void NoValues_EmbededValuesUsed()
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
            //Helm Config
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
            //Additional Package
            Variables.Set(SpecialVariables.Packages.PackageId("Pack-1"), "CustomValues");
            Variables.Set(SpecialVariables.Packages.PackageVersion("Pack-1"), "2.0.0");
            Variables.Set(SpecialVariables.Packages.OriginalPath("Pack-1"), GetFixtureResouce("Charts", "CustomValues.2.0.0.zip"));
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
        [RequiresNonMacAttribute]
        [Category(TestCategory.PlatformAgnostic)]
        public void ValuesFromPackageAndExplicit_ExplicitTakesPrecedence()
        {
            //Helm Config (lets make sure Explicit values take precedence
            Variables.Set(Kubernetes.SpecialVariables.Helm.KeyValues, "{\"SpecialMessage\": \"FooBar\"}");

            //Additional Package
            Variables.Set(SpecialVariables.Packages.PackageId("Pack-1"), "CustomValues");
            Variables.Set(SpecialVariables.Packages.PackageVersion("Pack-1"), "2.0.0");
            Variables.Set(SpecialVariables.Packages.OriginalPath("Pack-1"),
                GetFixtureResouce("Charts", "CustomValues.2.0.0.zip"));
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
            var version = "2.9.1";
            var platformFile = CalamariEnvironment.IsRunningOnWindows ?  "windows-amd64" : "linux-amd64";
            var fileName = Path.Combine(Path.GetTempPath(), $"helm-v{version}-{platformFile}.tgz");
            using (new TemporaryFile(fileName))
            {
                using (var myWebClient = new WebClient())
                {
                    myWebClient.DownloadFile($"https://storage.googleapis.com/kubernetes-helm/helm-v{version}-{platformFile}.tar.gz", fileName);
                }

                var customHelmExePackageId = Kubernetes.SpecialVariables.Helm.Packages.CustomHelmExePackageKey;
                Variables.Set(SpecialVariables.Packages.OriginalPath(customHelmExePackageId), fileName);
                Variables.Set(SpecialVariables.Packages.Extract(customHelmExePackageId), "True");
                Variables.Set(SpecialVariables.Packages.PackageId(customHelmExePackageId), "helmexe");
                Variables.Set(SpecialVariables.Packages.PackageVersion(customHelmExePackageId), version);

                //If package is provided then it should be treated as a relative path
                var customLocation = platformFile + Path.DirectorySeparatorChar +"helm";
                Variables.Set(Kubernetes.SpecialVariables.Helm.CustomHelmExecutable, customLocation);

                var result = DeployPackage();
                result.AssertSuccess();
                result.AssertOutput("Using custom helm executable at");
            }
        }
        
        [Test]
        [RequiresNonFreeBSDPlatform]
        [RequiresNon32BitWindows]
        [RequiresNonMacAttribute]
        [Category(TestCategory.PlatformAgnostic)]
        public void TillerNamespace_CannotFindIfRandomNamespaceUsed()
        {   
            // We're basically just testing here that setting the tiller namespace does put the param into the cmd
            Variables.Set(Kubernetes.SpecialVariables.Helm.TillerNamespace, "random-foobar");

            var result = DeployPackage();
            
            result.AssertFailure();
            result.AssertErrorOutput("Error: could not find tiller");
        }

        void AddPostDeployMessageCheckAndCleanup()
        {
            var kubectlCmd = "kubectl get configmaps " + ConfigMapName + " --namespace " + Namespace +" -o jsonpath=\"{.data.myvalue}\"";
            var syntax = ScriptSyntax.Bash;
            var script = "set_octopusvariable Message \"$("+ kubectlCmd +")\"\nhelm delete "+ ReleaseName +" --purge";
            if (CalamariEnvironment.IsRunningOnWindows)
            {
                syntax = ScriptSyntax.PowerShell;
                script = $"Set-OctopusVariable -name Message -Value $({kubectlCmd})\r\nhelm delete {ReleaseName} --purge";
            }

            Variables.Set(SpecialVariables.Action.CustomScripts.GetCustomScriptStage(DeploymentStages.PostDeploy, syntax), script);
            Variables.Set(SpecialVariables.Package.EnabledFeatures, SpecialVariables.Features.CustomScripts);
        }

        CalamariResult DeployPackage()
        {
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