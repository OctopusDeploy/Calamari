using System;
using System.IO;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Tests.Helpers;
using  Calamari.Integration.Processes;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.KubernetesFixtures
{
    [TestFixture]
    
    public class HelmUpgradeFixture: CalamariFixture
    {
        private static readonly string ServerUrl = Environment.GetEnvironmentVariable("K8S_OctopusAPITester_Server");
        static readonly string ClusterToken = Environment.GetEnvironmentVariable("K8S_OctopusAPITester_Token");
        
        protected ICalamariFileSystem FileSystem { get; private set; }
        protected VariableDictionary Variables { get; private set; }
        protected string StagingDirectory { get; private set; }

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
            
            // K8S Config
            Variables.Set(Kubernetes.SpecialVariables.ClusterUrl, ServerUrl);
            Variables.Set(Kubernetes.SpecialVariables.SkipTlsVerification, "True");
            Variables.Set(Kubernetes.SpecialVariables.Namespace, "calamari-testing");
            Variables.Set(SpecialVariables.Account.AccountType, "Token");
            Variables.Set(SpecialVariables.Account.Token, ClusterToken);
        }

        [Test]
        public void DoIt()
        {
            //Helm Config
            Variables.Set(Kubernetes.SpecialVariables.Helm.Install, "True");
            Variables.Set(Kubernetes.SpecialVariables.Helm.ReleaseName, "mynewrelease");
            
            var res = DeployPackage("mychart-0.2.0.tgz");

            //res.AssertOutputMatches("NAME:   mynewrelease"); //Does not appear on upgrades, only installs
            res.AssertOutputMatches("NAMESPACE: calamari-testing");
            res.AssertOutputMatches("STATUS: DEPLOYED");
            res.AssertOutputMatches("mychart-configmap");
            res.AssertSuccess();
        }
        
        protected CalamariResult DeployPackage(string chartName)
        {
            using (var variablesFile = new TemporaryFile(Path.GetTempFileName()))
            {
                var pkg = GetFixtureResouce("Charts", chartName);
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