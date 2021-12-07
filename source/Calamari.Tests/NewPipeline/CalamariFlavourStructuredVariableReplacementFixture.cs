using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Calamari.Commands.Support;
using Calamari.Common;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.NewPipeline
{
    /// <summary>
    /// Ensures that in both CalamariFlavour programs, we can fall back to the correct file replacer
    /// for Structured Variable Replacement when the file extension of the target file doesn't match the
    /// canonical extension for the replacer
    /// </summary>
    /// <remarks>
    /// We don't support content-based fallback for Java Properties files. They must have the .properties extension.
    ///
    /// Because of the lack of an actual standard for .properties file (it's basically a glorified set of
    /// key/value pairs, with very loose rules around content), it's not possible to distinguish between
    /// a YAML and a Properties file via parsers alone. Properties files will (almost) always succeed in
    /// the YAML parser. Given  the relative improbability of needing to replace a properties file that
    /// doesn't have the .properties extension, and the relative ubiquity of YAML under a variety of
    /// extensions, this is a known and accepted constraint.
    /// </remarks>
    [TestFixture]
    public class CalamariFlavourProgramStructuredVariableReplacementFixture
    {
        [Test]
        [TestCase("web.config", "/app/port", "Xml")]
        [TestCase("json.txt", "app:port", "Json")]
        [TestCase("yaml.txt", "app:port", "Yaml")]
        public void CalamariFlavourProgram_PerformsReplacementCorrectlyWithoutCanonicalFileExtension(string configFileName, string variableName, string expectedReplacer)
        {
            const string newPort = "4444";
            using (var tempPath = TemporaryDirectory.Create())
            {
                var targetPath = Path.Combine(tempPath.DirectoryPath, configFileName);
                File.Copy(BuildConfigPath(configFileName), targetPath);
                
                CommandTestBuilder.Create<NoOpTraditionalCommand, SyncFlavourProgram>()
                                        .WithArrange(context =>
                                                     {
                                                         context.Variables.Add(KnownVariables.Package.EnabledFeatures, KnownVariables.Features.StructuredConfigurationVariables);
                                                         context.Variables.Add(ActionVariables.StructuredConfigurationVariablesTargets, configFileName);
                                                         context.Variables.Add(variableName, newPort);
                                                         context.WithFilesToCopy(tempPath.DirectoryPath);
                                                     })
                                        .WithAssert(result =>
                                                    {
                                                        result.FullLog.Should().Contain($"Structured variable replacement succeeded on file {targetPath} with format {expectedReplacer}");
                                                        File.ReadAllText(targetPath).Should().Contain(newPort);
                                                    })
                                        .Execute();
            }
        }
        
        [Test]
        [TestCase("web.config", "/app/port", "Xml")]
        [TestCase("json.txt", "app:port", "Json")]
        [TestCase("yaml.txt", "app:port", "Yaml")]
        public async Task CalamariFlavourProgramAsync_PerformsReplacementCorrectlyWithoutCanonicalFileExtension(string configFileName, string variableName, string expectedReplacer)
        {
            const string newPort = "4444";
            using (var tempPath = TemporaryDirectory.Create())
            {
                var targetPath = Path.Combine(tempPath.DirectoryPath, configFileName);
                File.Copy(BuildConfigPath(configFileName), targetPath);
                
                await CommandTestBuilder.CreateAsync<NoOpPipelineCommand, AsyncFlavourProgram>()
                                        .WithArrange(context =>
                                                     {
                                                         context.Variables.Add(KnownVariables.Package.EnabledFeatures, KnownVariables.Features.StructuredConfigurationVariables);
                                                         context.Variables.Add(ActionVariables.StructuredConfigurationVariablesTargets, configFileName);
                                                         context.Variables.Add(variableName, newPort);
                                                         context.WithFilesToCopy(tempPath.DirectoryPath);
                                                     })
                                        .WithAssert(result =>
                                                    {
                                                        result.FullLog.Should().Contain($"Structured variable replacement succeeded on file {targetPath} with format {expectedReplacer}");
                                                        File.ReadAllText(targetPath).Should().Contain(newPort);
                                                    })
                                        .Execute();
            }
        }

        static string BuildConfigPath(string filename)
        {
            var path = typeof(CalamariFlavourProgramStructuredVariableReplacementFixture).Namespace.Replace("Calamari.Tests.", string.Empty);
            path = path.Replace('.', Path.DirectorySeparatorChar);
            var workingDirectory = Path.Combine(TestEnvironment.CurrentWorkingDirectory, path, "Config");

            if (string.IsNullOrEmpty(filename))
                return workingDirectory;

            return Path.Combine(workingDirectory, filename.Replace('\\', Path.DirectorySeparatorChar));
        }

        class SyncFlavourProgram : CalamariFlavourProgram
        {
            public SyncFlavourProgram(ILog log) : base(log)
            {
            }
        }
        
        class AsyncFlavourProgram : CalamariFlavourProgramAsync
        {
            public AsyncFlavourProgram(ILog log) : base(log)
            {

            }
        }

        [Command("no-op-command")]
        class NoOpPipelineCommand : PipelineCommand
        {
            protected override IEnumerable<IDeployBehaviour> Deploy(DeployResolver resolver)
            {
                yield return resolver.Create<EmptyBehaviour>();
            }
        }

        [Command("no-op-command")]
        class NoOpTraditionalCommand : Command, ICommand
        {
            public override int Execute(string[] commandLineArguments)
            {
                return Execute();
            }

            public int Execute()
            {
                return 0;
            }
        }
        
        class EmptyBehaviour : IDeployBehaviour
        {
            public bool IsEnabled(RunningDeployment context)
            {
                return false;
            }

            public Task Execute(RunningDeployment context)
            {
                throw new NotImplementedException();
            }
        }
    }
}