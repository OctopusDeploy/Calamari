#if JAVA_SUPPORT

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Shared;
using Calamari.Shared.FileSystem;
using Calamari.Tests.Helpers;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.Fixtures.Deployment
{
    [TestFixture]
    public class DeployJavaArchiveFixture : CalamariFixture
    {
        protected VariableDictionary Variables { get; private set; }
        protected ICalamariFileSystem FileSystem { get; private set; }
        protected string ApplicationDirectory { get; private set; }


        [SetUp]
        public virtual void SetUp()
        {
            FileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();

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
        }

        [Test]
        public void EnsureMetafileDataRepacked()
        {
            var targetFile = Path.Combine(ApplicationDirectory, "HelloWorld", "0.0.1", "HelloWorld.0.0.1.jar");
            
            var result = DeployPackage(TestEnvironment.GetTestPath("Fixtures", "Deployment", "Packages", "HelloWorld.0.0.1.jar"));
            
            result.AssertSuccess();
            result.AssertOutput($"Re-packaging archive: '{targetFile}'");
            
            //Check the manifest is copied from the original
            using (var stream = new FileStream(targetFile, FileMode.Open))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                var manifestEntry = archive.Entries.First(e => e.FullName == "META-INF/MANIFEST.MF");
                using (var reader = new StreamReader(manifestEntry.Open()))
                {
                    var manifest = reader.ReadToEnd();

                    Assert.That(manifest.Contains("CustomProperty: Foo"));
                }
            }
        }

        [Test]
        public void CanTransformConfigInJar()
        {
            const string configFile = "config.properties";
            Variables.Set(SpecialVariables.Package.SubstituteInFilesEnabled, true.ToString());
            Variables.Set(SpecialVariables.Package.SubstituteInFilesTargets, configFile);

            var result = DeployPackage(TestEnvironment.GetTestPath("Fixtures", "Deployment", "Packages", "HelloWorld.0.0.1.jar"));
            result.AssertSuccess();
            result.AssertOutput($"Performing variable substitution on '{Path.Combine(Environment.CurrentDirectory, "staging", configFile)}'");
        }

        protected CalamariResult DeployPackage(string packageName)
        {
            
            using (var variablesFile = new TemporaryFile(Path.GetTempFileName()))
            {
                Variables.Save(variablesFile.FilePath);

                return InvokeInProcess(Calamari()
                    .Action("deploy-java-archive")
                    .Argument("archive", packageName)
                    .Argument("variables", variablesFile.FilePath));
            }
        }
    }
}
#endif