using System;
using System.IO;
using Autofac;
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
    [Category(TestEnvironment.CompatibleOS.Windows)] //Lets see how badly multiple instances clash
    //[Ignore("Need kubectl & helm on server")]
    public class HelmUpgradeFixture: CalamariFixture
    {
        private static readonly string ServerUrl = Environment.GetEnvironmentVariable("K8S_OctopusAPITester_Server");
        static readonly string ClusterToken = Environment.GetEnvironmentVariable("K8S_OctopusAPITester_Token");

        private ICalamariFileSystem FileSystem { get; set; }
        private VariableDictionary Variables { get; set; }
        private string StagingDirectory { get; set; }
        private static readonly string ReleaseName = "calamaritest-"+ Guid.NewGuid().ToString("N").Substring(0, 6);
        private static readonly  string ConfigMapName = "mychart-configmap-" + ReleaseName; //Might clash with concurrent exections. Should make this dynamic
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

            Environment.SetEnvironmentVariable("TentacleJournal", Path.Combine(StagingDirectory, "DeploymentJournal.xml"));
            Variables = new VariableDictionary();
            Variables.EnrichWithEnvironmentVariables();
            Variables.Set(SpecialVariables.Tentacle.Agent.ApplicationDirectoryPath, StagingDirectory);

            //Chart Pckage
            Variables.Set(SpecialVariables.Package.NuGetPackageId, "mychart");
            Variables.Set(SpecialVariables.Package.NuGetPackageVersion, "0.3.7");
            
            //Helm Options
            Variables.Set(Kubernetes.SpecialVariables.Helm.ReleaseName, ReleaseName);
           
            //K8S Auth
            AddK8SAuth(Variables);
        }

        private void AddK8SAuth(VariableDictionary variables)
        {
            variables.Set(Kubernetes.SpecialVariables.ClusterUrl, ServerUrl);
            variables.Set(Kubernetes.SpecialVariables.SkipTlsVerification, "True");
            variables.Set(Kubernetes.SpecialVariables.Namespace, Namespace);
            variables.Set(SpecialVariables.Account.AccountType, "Token");
            variables.Set(SpecialVariables.Account.Token, ClusterToken);
        }

        [TearDown]
        public void RemoveTheCreatedRelease()
        {
            var variables = new VariableDictionary();
            variables.EnrichWithEnvironmentVariables();
            variables.Set(SpecialVariables.Tentacle.Agent.ApplicationDirectoryPath, StagingDirectory);
            AddK8SAuth(variables);
            variables.Set(SpecialVariables.Action.Script.ScriptBody, $"helm delete {ReleaseName} --purge");
            variables.Set(SpecialVariables.Action.Script.Syntax,
                (CalamariEnvironment.IsRunningOnWindows ? ScriptSyntax.PowerShell : ScriptSyntax.Bash).ToString());

            using (var variablesFile = new TemporaryFile(Path.GetTempFileName()))
            {
                variables.Save(variablesFile.FilePath);
                Invoke(Calamari()
                    .Action("run-script")
                    .Argument("extensions", "Calamari.Kubernetes")
                    .Argument("variables", variablesFile.FilePath))
                    .AssertSuccess();
            }
        }

        [Test]
        [RequiresNonFreeBSDPlatform]
        [RequiresNon32BitWindows]
        [RequiresNonMacAttribute]
        public void Upgrade_Succeeds()
        {
            var result = DeployPackage();

            //res.AssertOutputMatches("NAME:   mynewrelease"); //Does not appear on upgrades, only installs
            result.AssertOutputMatches($"NAMESPACE: {Namespace}");
            result.AssertOutputMatches("STATUS: DEPLOYED");
            result.AssertOutputMatches(ConfigMapName);
            result.AssertSuccess();
            Assert.AreEqual(ReleaseName.ToLower(), result.CapturedOutput.OutputVariables["ReleaseName"]);
        }
        
        [Test]
        [RequiresNonFreeBSDPlatform]
        [RequiresNon32BitWindows]
        public void NoValues_EmbededValuesUsed()
        {
            AddPostDeployMessageCheck();
            
            var result = DeployPackage();
            result.AssertSuccess();
            Assert.AreEqual("Hello Embedded Variables", result.CapturedOutput.OutputVariables["Message"]);
        }
        
        [Test]
        [RequiresNonFreeBSDPlatform]
        [RequiresNon32BitWindows]
        [RequiresNonMacAttribute]
        public void ExplicitValues_NewValuesUsed()
        {
            //Helm Config
            Variables.Set(Kubernetes.SpecialVariables.Helm.KeyValues, "{\"SpecialMessage\": \"FooBar\"}");
            AddPostDeployMessageCheck();
            
            var result = DeployPackage();
            result.AssertSuccess();
            Assert.AreEqual("Hello FooBar", result.CapturedOutput.OutputVariables["Message"]);
        }
        
        [Test]
        [RequiresNonFreeBSDPlatform]
        [RequiresNon32BitWindows]
        [RequiresNonMacAttribute]
        public void ValuesFromPackage_NewValuesUsed()
        {
            //Helm Config
            Variables.Set(Kubernetes.SpecialVariables.Helm.KeyValues, "{\"SpecialMessage\": \"FooBar\"}");
            AddPostDeployMessageCheck();
            
            //Additional Package
            Variables.Set(SpecialVariables.Packages.PackageId("Pack-1"), "CustomValues");
            Variables.Set(SpecialVariables.Packages.PackageVersion("Pack-1"), "2.0.0");
            Variables.Set(SpecialVariables.Packages.OriginalPath("Pack-1"), GetFixtureResouce("Charts", "CustomValues.2.0.0.zip"));
            Variables.Set(Kubernetes.SpecialVariables.Helm.Packages.ValuesFilePath("Pack-1"), "values.yaml");
            
            //Variable that will replace packaged value in package
            Variables.Set("MySecretMessage","From A Variable Replaced In Package");
            
            var result = DeployPackage();
            result.AssertSuccess();
            Assert.AreEqual("Hello From A Variable Replaced In Package", result.CapturedOutput.OutputVariables["Message"]);
        }
        
        void AddPostDeployMessageCheck()
        {
            var kubectlCmd = "kubectl get configmaps " + ConfigMapName + " --namespace " + Namespace +" -o jsonpath=\"{.data.myvalue}\"";
            var syntax = ScriptSyntax.Bash;
            var script = $"set_octopusvariable Message ${kubectlCmd})";
            
            if (CalamariEnvironment.IsRunningOnWindows)
            {
                syntax = ScriptSyntax.PowerShell;
                script = $"Set-OctopusVariable -name Message -Value $({kubectlCmd})";
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

                return Invoke(Calamari()
                    .Action("helm-upgrade")
                    .Argument("extensions", "Calamari.Kubernetes")
                    .Argument("package", pkg)
                    .Argument("variables", variablesFile.FilePath));
            }
        }
    }
}