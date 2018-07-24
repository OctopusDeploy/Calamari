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
            
            Variables = new VariableDictionary();
            Variables.EnrichWithEnvironmentVariables();
            Variables.Set(SpecialVariables.Tentacle.Agent.ApplicationDirectoryPath, StagingDirectory);
            Variables.Set(SpecialVariables.Package.PackageId, "mychart");
            
            // K8S Config
            Variables.Set(Kubernetes.SpecialVariables.ClusterUrl, ServerUrl);
            Variables.Set(Kubernetes.SpecialVariables.SkipTlsVerification, "True");
            Variables.Set(Kubernetes.SpecialVariables.Namespace, Namespace);
            Variables.Set(SpecialVariables.Account.AccountType, "Token");
            Variables.Set(SpecialVariables.Account.Token, ClusterToken);
        }

        [Test]
        public void Upgrade_Suceeds()
        {
            //Helm Config
            Variables.Set(Kubernetes.SpecialVariables.Helm.Install, "True");
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
            Variables.Set(Kubernetes.SpecialVariables.Helm.Install, "True");
            Variables.Set(Kubernetes.SpecialVariables.Helm.ReleaseName, "mynewrelease");
            
            DeployPackage("mychart-0.3.1.tgz").AssertSuccess();

            var configMapValue = GetConfigMapValue();
            Assert.AreEqual(configMapValue, "Hello Embedded Variables");
        }
        
        [Test]
        public void ExplicitValues_NewValuesUsed()
        {
            //Helm Config
            Variables.Set(Kubernetes.SpecialVariables.Helm.Install, "True");
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
            Variables.Set(Kubernetes.SpecialVariables.Helm.Install, "True");
            Variables.Set(Kubernetes.SpecialVariables.Helm.ReleaseName, "mynewrelease");
            Variables.Set(Kubernetes.SpecialVariables.Helm.KeyValues, "{\"SpecialMessage\": \"FooBar\"}");
            
            Variables.Set(SpecialVariables.Packages.PackageId("Pack-1"), "fakePackage");
            Variables.Set(SpecialVariables.Packages.OriginalPath("Pack-1"), GetFixtureResouce("Charts", "CustomValues.2.0.0.zip"));
            Variables.Set(Kubernetes.SpecialVariables.Helm.Packages.PerformVariableReplace("Pack-1"), "True");
            Variables.Set(Kubernetes.SpecialVariables.Helm.Packages.ValuesFilePath("Pack-1"), "values.yaml");
            Variables.Set("MySecretMessage","From A Variable Replaced In Package");
                
            DeployPackage("mychart-0.3.1.tgz").AssertSuccess();

            var configMapValue = GetConfigMapValue();
            Assert.AreEqual("Hello FooBar", configMapValue);
        }
        
        

        string GetConfigMapValue()
        {
            using (var variablesFile = new TemporaryFile(Path.GetTempFileName()))
            {
                var kubectlCmd = "kubectl get configmaps " + ConfigMapName + " --namespace " + Namespace +" -o jsonpath=\"{.data.myvalue}\"";
                var clonedVariables = new VariableDictionary();
                Variables.GetNames().ForEach(v => clonedVariables.Set(v, Variables.GetRaw(v)));
                clonedVariables.Set(SpecialVariables.Action.Script.ScriptBody, $"Set-OctopusVariable -name Message -Value $({kubectlCmd})");
                clonedVariables.Set(SpecialVariables.Action.Script.Syntax, ScriptSyntax.PowerShell.ToString());
                clonedVariables.Save(variablesFile.FilePath);
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
            /*
             *  [SetUp]
        public void SetUp()
        {
            ExternalVariables.LogMissingVariables();
            container = Calamari.Program.BuildContainer(Args);
        }

        [TearDown]
        public void TearDown()
        {
            container?.Dispose();
        }

        [Test]
        public void RunScript()
        {
            var retCode = container.Resolve<Calamari.Program>().Execute(Args);
            Assert.AreEqual(0, retCode);
        }
             * 
             */

            
            
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
//                
                return Invoke(Calamari()
                    .Action("helm-upgrade")
                    .Argument("extensions", "Calamari.Kubernetes")
                    .Argument("package", pkg)
                    .Argument("variables", variablesFile.FilePath));
            }
        }
    }
}