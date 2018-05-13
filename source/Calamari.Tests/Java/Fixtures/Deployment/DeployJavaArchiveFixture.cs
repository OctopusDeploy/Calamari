#if JAVA_SUPPORT 
using System;
using System.IO;
using System.Linq;
using Calamari.Commands.Java;
using Calamari.Deployment;
using Calamari.Hooks;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Calamari.Tests.Helpers;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.Java.Fixtures.Deployment
{
    [TestFixture]
    public class DeployJavaArchiveFixture : CalamariFixture
    {
        protected VariableDictionary Variables { get; private set; }
        protected ICalamariFileSystem FileSystem { get; private set; }
        protected string ApplicationDirectory { get; private set; }

        protected int ReturnCode { get; set; }

        protected ProxyLog ProxyLog { get; set; }


        [SetUp]
        public virtual void SetUp()
        {
            FileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
            ProxyLog = new ProxyLog();

            // Ensure staging directory exists and is empty 
            ApplicationDirectory = Path.Combine(Path.GetTempPath(), "CalamariTestStaging");
            FileSystem.EnsureDirectoryExists(ApplicationDirectory);
            FileSystem.PurgeDirectory(ApplicationDirectory, FailureOptions.ThrowOnFailure);

            Environment.SetEnvironmentVariable("TentacleJournal", Path.Combine(ApplicationDirectory, "DeploymentJournal.xml"));

            Variables = new VariableDictionary();
            Variables.EnrichWithEnvironmentVariables();
            Variables.Set(SpecialVariables.Tentacle.Agent.ApplicationDirectoryPath, ApplicationDirectory);
        }

        [TearDown]
        public virtual void CleanUp()
        {
            CalamariPhysicalFileSystem.GetPhysicalFileSystem().PurgeDirectory(ApplicationDirectory, FailureOptions.IgnoreFailure);
            ProxyLog.Dispose();
        }

        [Test]
        public void CanDeployJavaArchive()
        {
            DeployPackage(TestEnvironment.GetTestPath("Java", "Fixtures", "Deployment", "Packages", "HelloWorld.0.0.1.jar"));
            Assert.AreEqual(0, ReturnCode);

            //Archive is re-packed
            ProxyLog.AssertContains($"Re-packaging archive: '{Path.Combine(ApplicationDirectory, "HelloWorld", "0.0.1", "HelloWorld.0.0.1.jar")}'");
        }

        [Test]
        public void CanTransformConfigInJar()
        {
            const string configFile = "config.properties";
            Variables.Set(SpecialVariables.Package.SubstituteInFilesEnabled, true.ToString());
            Variables.Set(SpecialVariables.Package.SubstituteInFilesTargets, configFile);

            DeployPackage(TestEnvironment.GetTestPath("Java", "Fixtures", "Deployment", "Packages", "HelloWorld.0.0.1.jar"));
            Assert.AreEqual(0, ReturnCode);
            ProxyLog.AssertContains($"Performing variable substitution on '{Path.Combine(Environment.CurrentDirectory, "staging", configFile)}'");
        }

        protected void DeployPackage(string packageName)
        {
            using (var variablesFile = new TemporaryFile(Path.GetTempFileName()))
            {
                Variables.Save(variablesFile.FilePath);

                var command = new DeployJavaArchiveCommand(new CombinedScriptEngine());
                ReturnCode = command.Execute(new[] {"--archive", $"{packageName}", "--variables", $"{variablesFile.FilePath}" }); 
            }
        }
    }
}
#endif