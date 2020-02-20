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
using Calamari.Variables;
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
        IVariables variables;

        [SetUp]
        public void SetUp()
        {
            fileSystem = Substitute.For<ICalamariFileSystem>();
            substituter = Substitute.For<IFileSubstituter>();
            variables = new CalamariVariables();

            deployment = new RunningDeployment(TestEnvironment.ConstructRootedPath("packages"), variables)
            {
                StagingDirectory = StagingDirectory
            };
        }

        [Test]
        public void ShouldPerformSubstitutions()
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

            substituter.DidNotReceive().PerformSubstitution(Arg.Any<string>(), Arg.Any<IVariables>());
        }

        private SubstituteInFilesConvention CreateConvention()
        {
            return new SubstituteInFilesConvention(fileSystem, substituter);
        }

    }
}