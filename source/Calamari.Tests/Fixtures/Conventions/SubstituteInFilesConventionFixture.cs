using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Substitutions;
using NSubstitute;
using NSubstitute.Core;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.Fixtures.Conventions
{
    [TestFixture]
    public class SubstituteInFilesConventionFixture
    {
        ICalamariFileSystem fileSystem;
        IFileSubstituter substituter;
        RunningDeployment deployment;
        VariableDictionary variables;
        const string stagingDirectory = "C:\\Applications\\Acme";

        [SetUp]
        public void SetUp()
        {
            fileSystem = Substitute.For<ICalamariFileSystem>();
            substituter = Substitute.For<IFileSubstituter>();
            variables = new VariableDictionary();

            deployment = new RunningDeployment("C:\\packages", variables)
            {
                StagingDirectory = stagingDirectory
            };
        }

        [Test]
        public void ShouldPerformSubstitutions()
        {
            const string substitutionTarget = "subFolder\\config.json";

            fileSystem.EnumerateFiles(stagingDirectory, substitutionTarget)
                .Returns(new[] {Path.Combine(stagingDirectory, substitutionTarget)});

            variables.Set(SpecialVariables.Package.SubstituteInFilesTargets, substitutionTarget);
            variables.Set(SpecialVariables.Package.SubstituteInFilesEnabled, true.ToString());

            CreateConvention().Install(deployment);

            substituter.Received().PerformSubstitution(Path.Combine(stagingDirectory, substitutionTarget), variables);
        }

        [Test]
        public void ShouldNotSubstituteWhenFlagUnset()
        {
            const string substitutionTarget = "web.config";

            fileSystem.EnumerateFiles(stagingDirectory, substitutionTarget)
                .Returns(new[] {Path.Combine(stagingDirectory, substitutionTarget)});

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