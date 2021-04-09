using System;
using System.IO;
using Calamari.Common.Commands;
using Calamari.Common.Features.StructuredVariables;
using Calamari.Common.Plumbing.Deployment;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Tests.Helpers;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.StructuredVariables
{
    [TestFixture]
    public class StructuredConfigVariablesServiceFixture
    {
        const string FileName = "config.json";
        static readonly string AdditionalPath = Path.GetFullPath("/additionalPath/");
        static readonly string CurrentPath = Path.GetFullPath("/currentPath/");
        static readonly string ConfigFileInCurrentPath = Path.Combine(CurrentPath, FileName);
        static readonly string ConfigFileInAdditionalPath = Path.Combine(AdditionalPath, FileName);

        void RunAdditionalPathsTest(
            bool fileExistsInPath,
            bool fileExistsInAdditionalPath,
            Action<IFileFormatVariableReplacer> replacerAssertions = null,
            Action<InMemoryLog> logAssertions = null)
        {
            var fileSystem = Substitute.For<ICalamariFileSystem>();
            fileSystem.FileExists(ConfigFileInCurrentPath).Returns(fileExistsInPath);
            fileSystem.FileExists(ConfigFileInAdditionalPath).Returns(fileExistsInAdditionalPath);
            fileSystem.EnumerateFilesWithGlob(CurrentPath, FileName)
                      .Returns(fileExistsInPath ? new[]{ ConfigFileInCurrentPath } : new string[0]);
            fileSystem.EnumerateFilesWithGlob(AdditionalPath, FileName)
                      .Returns(fileExistsInAdditionalPath ? new[]{ ConfigFileInAdditionalPath } : new string[0]);

            var replacer = Substitute.For<IFileFormatVariableReplacer>();
            replacer.FileFormatName.Returns(StructuredConfigVariablesFileFormats.Json);
            replacer.IsBestReplacerForFileName(Arg.Any<string>()).Returns(true);

            var log = new InMemoryLog();
            var variables = new CalamariVariables();
            variables.Set(ActionVariables.AdditionalPaths, AdditionalPath);
            variables.Set(KnownVariables.Package.EnabledFeatures, KnownVariables.Features.StructuredConfigurationVariables);
            variables.Set(ActionVariables.StructuredConfigurationVariablesTargets, FileName);
            variables.Set(PackageVariables.CustomInstallationDirectory, CurrentPath);

            var service = new StructuredConfigVariablesService(new []
            {
                replacer
            }, variables, fileSystem, log);

            var deployment = new RunningDeployment(CurrentPath, variables)
            {
                CurrentDirectoryProvider = DeploymentWorkingDirectory.CustomDirectory
            };

            service.ReplaceVariables(deployment.CurrentDirectory);

            replacerAssertions?.Invoke(replacer);
            logAssertions?.Invoke(log);
        }

        [Test]
        public void ReplacesVariablesInAdditionalPathIfFileMatchedInWorkingDirectory()
        {
            void Assertions(IFileFormatVariableReplacer replacer)
            {
                replacer.Received().ModifyFile(ConfigFileInCurrentPath, Arg.Any<IVariables>());
                replacer.Received().ModifyFile(ConfigFileInAdditionalPath, Arg.Any<IVariables>());
            }

            RunAdditionalPathsTest(true, true, Assertions);
        }

        [Test]
        public void ReplacesVariablesInAdditionalPathIfFileNotMatchedInWorkingDirectory()
        {
            void Assertions(IFileFormatVariableReplacer replacer)
            {
                replacer.Received().ModifyFile(ConfigFileInAdditionalPath, Arg.Any<IVariables>());
            }

            RunAdditionalPathsTest(false, true, Assertions);
        }

        [Test]
        public void DoesntReplacesVariablesInAdditionalPathIfFileNotMatchedInAdditionalPath()
        {
            void Assertions(IFileFormatVariableReplacer replacer)
            {
                replacer.Received().ModifyFile(ConfigFileInCurrentPath, Arg.Any<IVariables>());
            }

            RunAdditionalPathsTest(true, false, Assertions);
        }

        [Test]
        public void LogAWarningIfNoMatchingFileIsFoundInAnyPath()
        {
            void Assertions(InMemoryLog log)
            {
                log.Messages.Should().Contain(message => message.FormattedMessage == $"No files were found that match the replacement target pattern '{FileName}'");
            }
            RunAdditionalPathsTest(false, false, logAssertions: Assertions);
        }
    }
}