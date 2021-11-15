using System;
using System.IO;
using Calamari.Common.Commands;
using Calamari.Common.Features.Substitutions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Tests.Helpers;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Deployment.Conventions
{
    [TestFixture]
    public class SubstituteInFilesFixture
    {
        static readonly string StagingDirectory = TestEnvironment.ConstructRootedPath("Applications", "Acme");

        [Test]
        public void ShouldPerformSubstitutions()
        {
            string glob = "**\\*config.json";
            string actualMatch = "config.json";


            var variables = new CalamariVariables();
            variables.Set(PackageVariables.SubstituteInFilesTargets, glob);
            variables.Set(KnownVariables.Package.EnabledFeatures, KnownVariables.Features.SubstituteInFiles);

            var deployment = new RunningDeployment(TestEnvironment.ConstructRootedPath("packages"), variables)
            {
                StagingDirectory = StagingDirectory
            };

            var fileSystem = Substitute.For<ICalamariFileSystem>();
            fileSystem.EnumerateFilesWithGlob(StagingDirectory, glob).Returns(new[] { Path.Combine(StagingDirectory, actualMatch) });

            var substituter = Substitute.For<IFileSubstituter>();
            new SubstituteInFiles(new InMemoryLog(), fileSystem, substituter, variables)
                .SubstituteBasedSettingsInSuppliedVariables(deployment.CurrentDirectory);

            substituter.Received().PerformSubstitution(Path.Combine(StagingDirectory, actualMatch), variables);
        }


    }
}