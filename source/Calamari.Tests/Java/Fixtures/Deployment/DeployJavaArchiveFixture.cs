#if JAVA_SUPPORT
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Calamari.Commands.Java;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Variables;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;
using Calamari.Integration.Processes;
using Calamari.Integration.Substitutions;
using Calamari.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.Java.Fixtures.Deployment
{
    [TestFixture]
    public class DeployJavaArchiveFixture : CalamariFixture
    {
        IVariables variables;
        ICalamariFileSystem fileSystem;
        string applicationDirectory;
        int returnCode;
        InMemoryLog log;


        [SetUp]
        public virtual void SetUp()
        {
            fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
            log = new InMemoryLog();

            // Ensure staging directory exists and is empty
            applicationDirectory = Path.Combine(Path.GetTempPath(), "CalamariTestStaging");
            fileSystem.EnsureDirectoryExists(applicationDirectory);
            fileSystem.PurgeDirectory(applicationDirectory, FailureOptions.ThrowOnFailure);

            Environment.SetEnvironmentVariable("TentacleJournal", Path.Combine(applicationDirectory, "DeploymentJournal.xml"));

            variables = new VariablesFactory(fileSystem).Create(new CommonOptions("test"));
            variables.Set(TentacleVariables.Agent.ApplicationDirectoryPath, applicationDirectory);
        }

        [TearDown]
        public virtual void CleanUp()
        {
            CalamariPhysicalFileSystem.GetPhysicalFileSystem().PurgeDirectory(applicationDirectory, FailureOptions.IgnoreFailure);
        }

        [Test]
        public void CanDeployJavaArchive()
        {
            DeployPackage(TestEnvironment.GetTestPath("Java", "Fixtures", "Deployment", "Packages", "HelloWorld.0.0.1.jar"));
            Assert.AreEqual(0, returnCode);

            //Archive is re-packed
            log.StandardOut.Should().Contain($"Re-packaging archive: '{Path.Combine(applicationDirectory, "HelloWorld", "0.0.1", "HelloWorld.0.0.1.jar")}'");
        }

        // https://github.com/OctopusDeploy/Issues/issues/4733
        [Test]
        public void EnsureMetafileDataRepacked()
        {
            DeployPackage(TestEnvironment.GetTestPath("Java", "Fixtures", "Deployment", "Packages", "HelloWorld.0.0.1.jar"));
            Assert.AreEqual(0, returnCode);

            var targetFile = Path.Combine(applicationDirectory, "HelloWorld", "0.0.1", "HelloWorld.0.0.1.jar");

            //Archive is re-packed
            log.StandardOut.Should().Contain($"Re-packaging archive: '{targetFile}'");

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
            variables.Set(PackageVariables.SubstituteInFilesEnabled, true.ToString());
            variables.Set(PackageVariables.SubstituteInFilesTargets, configFile);

            DeployPackage(TestEnvironment.GetTestPath("Java", "Fixtures", "Deployment", "Packages", "HelloWorld.0.0.1.jar"));
            Assert.AreEqual(0, returnCode);
            log.StandardOut.Should().Contain($"Performing variable substitution on '{Path.Combine(Environment.CurrentDirectory, "staging", configFile)}'");
        }

        protected void DeployPackage(string packageName)
        {
            var command = new DeployJavaArchiveCommand(
                log,
                new ScriptEngine(Enumerable.Empty<IScriptWrapper>()), 
                variables,
                fileSystem,
                new CommandLineRunner(log, variables),
                new SubstituteInFiles(log, fileSystem, new FileSubstituter(log, fileSystem), variables),
                new ExtractPackage(new CombinedPackageExtractor(log), fileSystem, variables, log)
            );
            returnCode = command.Execute(new[] { "--archive", $"{packageName}" });
        }
    }
}
#endif