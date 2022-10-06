#if JAVA_SUPPORT
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Calamari.Commands.Java;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.StructuredVariables;
using Calamari.Common.Features.Substitutions;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing.Helpers;
using Calamari.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.Java.Fixtures.Deployment
{
    [TestFixture]
    public class DeployJavaArchiveFixture : CalamariFixture
    {
        ICalamariFileSystem fileSystem;
        string applicationDirectory;
        int returnCode;
        InMemoryLog log;
        string sourcePackage;


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

            sourcePackage = TestEnvironment.GetTestPath("Java",
                                                            "Fixtures",
                                                            "Deployment",
                                                            "Packages",
                                                            "HelloWorld.0.0.1.jar");
        }

        [TearDown]
        public virtual void CleanUp()
        {
            CalamariPhysicalFileSystem.GetPhysicalFileSystem().PurgeDirectory(applicationDirectory, FailureOptions.IgnoreFailure);
        }

        [Test]
        public void CanDeployJavaArchive()
        {
            DeployPackage(sourcePackage, GenerateVariables());
            Assert.AreEqual(0, returnCode);

            //Archive is re-packed
            log.StandardOut.Should().Contain($"Re-packaging archive: '{Path.Combine(applicationDirectory, "HelloWorld", "0.0.1", "HelloWorld.0.0.1.jar")}'");
        }

        // https://github.com/OctopusDeploy/Issues/issues/4733
        [Test]
        public void EnsureMetafileDataRepacked()
        {
            DeployPackage(sourcePackage, GenerateVariables());
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
            var variables = GenerateVariables();
            variables.Set(KnownVariables.Package.EnabledFeatures, KnownVariables.Features.SubstituteInFiles);
            variables.Set(PackageVariables.SubstituteInFilesTargets, configFile);

            DeployPackage(sourcePackage, variables);
            Assert.AreEqual(0, returnCode);
            log.StandardOut.Should().Contain($"Performing variable substitution on '{Path.Combine(Environment.CurrentDirectory, "staging", configFile)}'");
        }

        [Test]
        public void CanDeployJavaArchiveUncompressed()
        {
            var variables = GenerateVariables();
            variables.Set(PackageVariables.JavaArchiveCompression, false.ToString());

            DeployPackage(sourcePackage, variables);
            Assert.AreEqual(0, returnCode);

            // Archive is re-packed
            var path = Path.Combine(applicationDirectory, "HelloWorld", "0.0.1", "HelloWorld.0.0.1.jar");
            log.StandardOut.Should().Contain($"Re-packaging archive: '{path}'");

            using (var stream = new FileStream(path, FileMode.Open))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                archive.Entries.All(a => a.CompressedLength == a.Length).Should().BeTrue();
            }
        }

        void DeployPackage(string packageName, IVariables variables)
        {
            var commandLineRunner = new CommandLineRunner(log, variables);
            var command = new DeployJavaArchiveCommand(
                log,
                new ScriptEngine(Enumerable.Empty<IScriptWrapper>()),
                variables,
                fileSystem,
                commandLineRunner,
                new SubstituteInFiles(log, fileSystem, new FileSubstituter(log, fileSystem), variables),
                new ExtractPackage(new CombinedPackageExtractor(log, variables, commandLineRunner), fileSystem, variables, log),
                new StructuredConfigVariablesService(new PrioritisedList<IFileFormatVariableReplacer>
                {
                    new JsonFormatVariableReplacer(fileSystem, log),
                    new XmlFormatVariableReplacer(fileSystem, log),
                    new YamlFormatVariableReplacer(fileSystem, log),
                    new PropertiesFormatVariableReplacer(fileSystem, log),
                }, variables, fileSystem, log)
            );
            returnCode = command.Execute(new[] { "--archive", $"{packageName}" });
        }

        protected IVariables GenerateVariables()
        {
            var variables = new VariablesFactory(fileSystem).Create(new CommonOptions("test"));
            variables.Set(TentacleVariables.Agent.ApplicationDirectoryPath, applicationDirectory);
            return variables;
        }
    }
}
#endif