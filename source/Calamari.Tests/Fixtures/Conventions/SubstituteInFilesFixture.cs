using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Variables;
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
            variables.Set(PackageVariables.SubstituteInFilesEnabled, true.ToString());

            var deployment = new RunningDeployment(TestEnvironment.ConstructRootedPath("packages"), variables)
            {
                StagingDirectory = StagingDirectory
            };
            
            var fileSystem = Substitute.For<ICalamariFileSystem>();
            fileSystem.EnumerateFilesWithGlob(StagingDirectory, glob).Returns(new[] { Path.Combine(StagingDirectory, actualMatch) });

            var substituter = Substitute.For<IFileSubstituter>();
            new SubstituteInFiles(fileSystem, substituter, variables)
                .SubstituteBasedSettingsInSuppliedVariables(deployment);

            substituter.Received().PerformSubstitution(Path.Combine(StagingDirectory, actualMatch), variables);
        }
        

    }
}