using System;
using System.IO;
using System.Linq;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Substitutions;
using Calamari.Tests.Helpers;
using NSubstitute;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.Fixtures.Conventions
{
    [TestFixture]
    [Category(TestEnvironment.CompatableOS.All)]
    public class SubstituteInFilesConventionFixture
    {
        static readonly string StagingDirectory = TestEnvironment.ConstructRootedPath("Applications", "Acme");

        ICalamariFileSystem fileSystem;
        IFileSubstituter substituter;
        RunningDeployment deployment;
        VariableDictionary variables;

        [SetUp]
        public void SetUp()
        {
            fileSystem = Substitute.For<ICalamariFileSystem>();
            substituter = Substitute.For<IFileSubstituter>();
            variables = new VariableDictionary();

            deployment = new RunningDeployment(TestEnvironment.ConstructRootedPath("packages"), variables)
            {
                StagingDirectory = StagingDirectory
            };
        }

        [Test]
        public void ShouldPerformSubstitutions()
        {
            string substitutionTarget = Path.Combine("subFolder","config.json");

            fileSystem.EnumerateFiles(StagingDirectory, substitutionTarget).Returns(new[] {Path.Combine(StagingDirectory, substitutionTarget)});

            variables.Set(SpecialVariables.Package.SubstituteInFilesTargets, substitutionTarget);
            variables.Set(SpecialVariables.Package.SubstituteInFilesEnabled, true.ToString());

            CreateConvention().Install(deployment);

            substituter.Received().PerformSubstitution(Path.Combine(StagingDirectory, substitutionTarget), variables);
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