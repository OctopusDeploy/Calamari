﻿#if JAVA_SUPPORT
using System;
using System.IO;
using System.IO.Compression;
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

        // https://github.com/OctopusDeploy/Issues/issues/4733
        [Test]
        public void EnsureMetafileDataRepacked()
        {
            DeployPackage(TestEnvironment.GetTestPath("Java", "Fixtures", "Deployment", "Packages", "HelloWorld.0.0.1.jar"));
            Assert.AreEqual(0, ReturnCode);

            var targetFile = Path.Combine(ApplicationDirectory, "HelloWorld", "0.0.1", "HelloWorld.0.0.1.jar");

            //Archive is re-packed
            ProxyLog.AssertContains($"Re-packaging archive: '{targetFile}'");

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

            DeployPackage(TestEnvironment.GetTestPath("Java", "Fixtures", "Deployment", "Packages", "HelloWorld.0.0.1.jar"));
            Assert.AreEqual(0, ReturnCode);
            ProxyLog.AssertContains($"Performing variable substitution on '{Path.Combine(Environment.CurrentDirectory, "staging", configFile)}'");
        }

//        [Test]
//        public void CanDeployToTomcat()
//        {
//            Variables.Set(SpecialVariables.Package.EnabledFeatures, SpecialVariables.Action.Java.Tomcat.Feature);
//            Variables.Set(SpecialVariables.Action.Java.Tomcat.DeployName, "foo");
//            Variables.Set(SpecialVariables.Action.Java.Tomcat.Controller, "http://localhost:8080/manager");
//            Variables.Set(SpecialVariables.Action.Java.Tomcat.User, "tomcat");
//            Variables.Set(SpecialVariables.Action.Java.Tomcat.Password, "xxx");
//            Variables.Set(SpecialVariables.Action.Java.Tomcat.Enabled, "true");
//
//            Variables.Set(SpecialVariables.Action.Java.JavaLibraryEnvVar,
//                @"C:\Octopus\LocalTentacle\Tools\Octopus.Dependencies.Java\1.0.101");
//            
//            DeployPackage(TestEnvironment.GetTestPath("Java", "Fixtures", "Deployment", "Packages", "HelloWorld.0.0.1.jar"));
//        }

        protected void DeployPackage(string packageName)
        {
            using (var variablesFile = new TemporaryFile(Path.GetTempFileName()))
            {
                Variables.Save(variablesFile.FilePath);

                var command = new DeployJavaArchiveCommand(new CombinedScriptEngine());
                ReturnCode = command.Execute(new[] { "--archive", $"{packageName}", "--variables", $"{variablesFile.FilePath}" });
            }
        }
    }
}
#endif