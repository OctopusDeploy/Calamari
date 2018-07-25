using System;
using System.IO;
using Autofac;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Tests.Helpers;
using  Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.KubernetesFixtures
{
    [TestFixture]
    [Ignore("Not yet")]
    public class HelmUpgradeFixture: CalamariFixture
    {
        private static readonly string ServerUrl = Environment.GetEnvironmentVariable("K8S_OctopusAPITester_Server");
        static readonly string ClusterToken = Environment.GetEnvironmentVariable("K8S_OctopusAPITester_Token");
        
        protected ICalamariFileSystem FileSystem { get; private set; }
        protected VariableDictionary Variables { get; private set; }
        protected string StagingDirectory { get; private set; }
        const string ConfigMapName = "mychart-configmap"; //Might clash with concurrent exections. Should make this dynamic
        private const string Namespace = "calamari-testing";
        [SetUp]
        public void SetUp()
        {
            FileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();

            // Ensure staging directory exists and is empty 
            StagingDirectory = Path.Combine(Path.GetTempPath(), "CalamariTestStaging");
            FileSystem.EnsureDirectoryExists(StagingDirectory);
            FileSystem.PurgeDirectory(StagingDirectory, FailureOptions.ThrowOnFailure);

            Environment.SetEnvironmentVariable("TentacleJournal", Path.Combine(StagingDirectory, "DeploymentJournal.xml"));

            Variables = BaseRequiredVariableDictionary();
            AddChartPackage(Variables);
            AddK8SAuth(Variables);
        }

        VariableDictionary BaseRequiredVariableDictionary()
        {
            var variables = new VariableDictionary();
            variables.EnrichWithEnvironmentVariables();
            variables.Set(SpecialVariables.Tentacle.Agent.ApplicationDirectoryPath, StagingDirectory);
            return variables;
        }
        
        void AddK8SAuth(VariableDictionary variables)
        {
            variables.Set(Kubernetes.SpecialVariables.ClusterUrl, ServerUrl);
            variables.Set(Kubernetes.SpecialVariables.SkipTlsVerification, "True");
            variables.Set(Kubernetes.SpecialVariables.Namespace, Namespace);
            variables.Set(SpecialVariables.Account.AccountType, "Token");
            variables.Set(SpecialVariables.Account.Token, ClusterToken);
        }

        void AddChartPackage(VariableDictionary variables)
        {
            variables.Set(SpecialVariables.Package.NuGetPackageId, "mychart");
            variables.Set(SpecialVariables.Package.NuGetPackageVersion, "0.3.1");
        }

        [Test]
        public void Upgrade_Suceeds()
        {
            //Helm Config
            Variables.Set(Kubernetes.SpecialVariables.Helm.ReleaseName, "mynewrelease");
            
            var res = DeployPackage("mychart-0.3.1.tgz");

            //res.AssertOutputMatches("NAME:   mynewrelease"); //Does not appear on upgrades, only installs
            res.AssertOutputMatches("NAMESPACE: calamari-testing");
            res.AssertOutputMatches("STATUS: DEPLOYED");
            res.AssertOutputMatches("mychart-configmap");
            res.AssertSuccess();
        }
        
        [Test]
        public void NoValues_EmbededValuesUsed()
        {
            //Helm Config
            Variables.Set(Kubernetes.SpecialVariables.Helm.ReleaseName, "mynewrelease");
            
            DeployPackage("mychart-0.3.1.tgz").AssertSuccess();

            var configMapValue = GetConfigMapValue();
            Assert.AreEqual(configMapValue, "Hello Embedded Variables");
        }
        
        [Test]
        public void ExplicitValues_NewValuesUsed()
        {
            //Helm Config
            Variables.Set(Kubernetes.SpecialVariables.Helm.ReleaseName, "mynewrelease");
            Variables.Set(Kubernetes.SpecialVariables.Helm.KeyValues, "{\"SpecialMessage\": \"FooBar\"}");
            
            DeployPackage("mychart-0.3.1.tgz").AssertSuccess();

            var configMapValue = GetConfigMapValue();
            Assert.AreEqual("Hello FooBar", configMapValue);
        }
        
        
        [Test]
        public void ValuesFromPackage_NewValuesUsed()
        {
            //Helm Config
            Variables.Set(Kubernetes.SpecialVariables.Helm.ReleaseName, "mynewrelease");
            Variables.Set(Kubernetes.SpecialVariables.Helm.KeyValues, "{\"SpecialMessage\": \"FooBar\"}");
            
            Variables.Set(SpecialVariables.Packages.PackageId("Pack-1"), "CustomValues");
            Variables.Set(SpecialVariables.Packages.PackageVersion("Pack-1"), "2.0.0");
            Variables.Set(SpecialVariables.Packages.OriginalPath("Pack-1"), GetFixtureResouce("Charts", "CustomValues.2.0.0.zip"));
            Variables.Set(Kubernetes.SpecialVariables.Helm.Packages.PerformVariableReplace("Pack-1"), "True");
            Variables.Set(Kubernetes.SpecialVariables.Helm.Packages.ValuesFilePath("Pack-1"), "values.yaml");
            Variables.Set("MySecretMessage","From A Variable Replaced In Package");
                
            DeployPackage("mychart-0.3.1.tgz").AssertSuccess();

            var configMapValue = GetConfigMapValue();
            Assert.AreEqual("Hello From A Variable Replaced In Package", configMapValue);
        }
        
        string GetConfigMapValue()
        {
            var kubectlCmd = "kubectl get configmaps " + ConfigMapName + " --namespace " + Namespace +" -o jsonpath=\"{.data.myvalue}\"";
            var scriptVariables = BaseRequiredVariableDictionary();
            AddK8SAuth(scriptVariables);
            scriptVariables.Set(SpecialVariables.Action.Script.ScriptBody, $"Set-OctopusVariable -name Message -Value $({kubectlCmd})");
            scriptVariables.Set(SpecialVariables.Action.Script.Syntax, ScriptSyntax.PowerShell.ToString());
            
            using (var variablesFile = new TemporaryFile(Path.GetTempFileName()))
            {
                scriptVariables.Save(variablesFile.FilePath);            
              var t = Invoke(Calamari()
                  .Action("run-script")
                  .Argument("extensions", "Calamari.Kubernetes")
                  .Argument("variables", variablesFile.FilePath));
              t.AssertSuccess();
              return t.CapturedOutput.OutputVariables["Message"];
          }
      }
      
      CalamariResult DeployPackage(string chartName)
      {
            using (var variablesFile = new TemporaryFile(Path.GetTempFileName()))
            {
                var pkg = GetFixtureResouce("Charts", chartName);
                Variables.Save(variablesFile.FilePath);

//                var args = new [] {"helm-upgrade",
//                    "--extensions", "Calamari.Kubernetes",
//                    "--package", pkg,
//                    "--variables", variablesFile.FilePath};
//                
//                var container = global::Calamari.Program.BuildContainer(args);
//                var t = container.Resolve<Calamari.Program>().Execute(args);
//                return null;

                return Invoke(Calamari()
                    .Action("helm-upgrade")
                    .Argument("extensions", "Calamari.Kubernetes")
                    .Argument("package", pkg)
                    .Argument("variables", variablesFile.FilePath));
            }
        }
    }
}