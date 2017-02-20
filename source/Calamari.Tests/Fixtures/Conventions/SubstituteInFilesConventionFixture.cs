using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Substitutions;
using Calamari.Tests.Helpers;
using NSubstitute;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.Fixtures.Conventions
{
    [TestFixture]
    public class SubstituteInFilesConventionFixture
    {
        static readonly string StagingDirectory = TestEnvironment.ConstructRootedPath("Applications", "Acme");

        ICalamariFileSystem fileSystem;
        IFileSubstituter substituter;
        RunningDeployment deployment;
        CalamariVariableDictionary variables;

        [SetUp]
        public void SetUp()
        {
            fileSystem = Substitute.For<ICalamariFileSystem>();
            substituter = Substitute.For<IFileSubstituter>();
            variables = new CalamariVariableDictionary();

            deployment = new RunningDeployment(TestEnvironment.ConstructRootedPath("packages"), variables)
            {
                StagingDirectory = StagingDirectory
            };
        }

        [Test]
        [TestCase(@"**/*.txt", "f1.txt", 2)]
        [TestCase(@"**/*.txt", "r.txt", 2)]
        [TestCase(@"*.txt", "r.txt")]
        [TestCase(@"**/*.config", "root.config", 5)]
        [TestCase(@"*.config", "root.config")]
        [TestCase(@"Config/*.config", "c.config")]
        [TestCase(@"Config/Feature1/*.config", "f1-a.config", 2)]
        [TestCase(@"Config/Feature1/*.config", "f1-b.config", 2)]
        [TestCase(@"Config/Feature2/*.config", "f2.config")]
        public void GlobTestMutiple(string pattern, string expectedFileMatchName, int expectedQty = 1)
        {
            var realFileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
            var rootPath = realFileSystem.CreateTemporaryDirectory();
            var content = "file-content" + Environment.NewLine;

            if (CalamariEnvironment.IsRunningOnWindows)
            {
                pattern = pattern.Replace(@"/", @"\");
            }

            try
            {
                var configPath = Path.Combine(rootPath, "Config");

                realFileSystem.CreateDirectory(configPath);
                realFileSystem.CreateDirectory(Path.Combine(configPath, "Feature1"));
                realFileSystem.CreateDirectory(Path.Combine(configPath, "Feature2"));

                Action<string, string, string> writeFile = (p1, p2, p3) => 
                    realFileSystem.OverwriteFile(p3 == null ? Path.Combine(p1, p2) : Path.Combine(p1, p2, p3), content);

                // NOTE: create all the files in *every case*, and TestCases help supply the assert expectations
                writeFile(rootPath, "root.config", null);
                writeFile(rootPath, "r.txt", null);
                writeFile(configPath, "c.config", null);

                writeFile(configPath, "Feature1", "f1.txt");
                writeFile(configPath, "Feature1", "f1-a.config");
                writeFile(configPath, "Feature1", "f1-b.config");
                writeFile(configPath, "Feature2", "f2.config");

                var result = Glob.Expand(Path.Combine(rootPath, pattern)).ToList();

                Assert.AreEqual(expectedQty, result.Count, $"{pattern} should have found {expectedQty}, but found {result.Count}");
                Assert.True(result.Any(r => r.Name.Equals(expectedFileMatchName)), $"{pattern} should have found {expectedFileMatchName}, but didn't");
            }
            finally
            {
                realFileSystem.DeleteDirectory(rootPath);
            }
        }

        [Test]
        public void ShouldPerformSubstitutionsWithGlobs()
        {
            string glob = "**\\*config.json";
            string actualMatch = "config.json";

            fileSystem.EnumerateFilesWithGlob(StagingDirectory, glob).Returns(new[] { Path.Combine(StagingDirectory, actualMatch) });

            variables.Set(SpecialVariables.Package.SubstituteInFilesTargets, glob);
            variables.Set(SpecialVariables.Package.SubstituteInFilesEnabled, true.ToString());

            CreateConvention().Install(deployment);

            substituter.Received().PerformSubstitution(Path.Combine(StagingDirectory, actualMatch), variables);
        }

        [Test]
        public void ShouldNotSubstituteWhenFlagUnset()
        {
            const string substitutionTarget = "web.config";

            fileSystem.EnumerateFiles(StagingDirectory, substitutionTarget)
                .Returns(new[] {Path.Combine(StagingDirectory, substitutionTarget)});

            variables.Set(SpecialVariables.Package.SubstituteInFilesTargets, substitutionTarget);
            variables.Set(SpecialVariables.Package.SubstituteInFilesEnabled, false.ToString());

            CreateConvention().Install(deployment);

            substituter.DidNotReceive().PerformSubstitution(Arg.Any<string>(), Arg.Any<VariableDictionary>());
        }

        private SubstituteInFilesConvention CreateConvention()
        {
            return new SubstituteInFilesConvention(fileSystem, substituter);
        }

    }
}