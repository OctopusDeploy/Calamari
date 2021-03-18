using System;
using System.IO;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Tests.Shared;
using FluentAssertions;
using NUnit.Framework;
using Sashimi.Server.Contracts;
using KnownVariables = Sashimi.Server.Contracts.KnownVariables;
using ScriptSyntax = Calamari.Common.Features.Scripts.ScriptSyntax;

namespace Calamari.Scripting.Tests
{
    [TestFixture]
    public class RunScriptCommandFixture
    {
        [Test]
        public Task ExecuteInlineScript()
        {
            var psScript = "echo \"Hello $Name #{Name2}\"";
            return CommandTestBuilder.CreateAsync<RunScriptCommand, Program>()
                                     .WithArrange(context =>
                                                  {
                                                      context.Variables.Add(KnownVariables.Action.Script.ScriptSource, KnownVariableValues.Action.Script.ScriptSource.Inline);
                                                      context.Variables.Add(KnownVariables.Action.Script.Syntax, ScriptSyntax.PowerShell.ToString());
                                                      context.Variables.Add(KnownVariables.Action.Script.ScriptBody, psScript);
                                                      context.Variables.Add("Name", "World");
                                                      context.Variables.Add("Name2", "Two");
                                                  })
                                     .WithAssert(result => result.FullLog.Should().Contain("Hello World Two"))
                                     .Execute();
        }

        [Test]
        public Task ExecuteWithPackage()
        {
            using var tempFolder = TemporaryDirectory.Create();
            var psScript = "echo \"Hello $Name #{Name2}\"";
            File.WriteAllText(Path.Combine(tempFolder.DirectoryPath, "myscript.ps1"), psScript);

            return CommandTestBuilder.CreateAsync<RunScriptCommand, Program>()
                                     .WithArrange(context =>
                                                  {
                                                      context.Variables.Add(KnownVariables.Action.Script.ScriptSource, KnownVariableValues.Action.Script.ScriptSource.Package);
                                                      context.Variables.Add("Octopus.Action.Script.ScriptFileName", "myscript.ps1");
                                                      context.Variables.Add("Name", "World");
                                                      context.Variables.Add("Name2", "Two");
                                                      context.WithFilesToCopy(tempFolder.DirectoryPath);
                                                  })
                                     .WithAssert(result => result.FullLog.Should().Contain("Hello World Two"))
                                     .Execute();
        }

        [Test]
        public Task ExecuteWithPackageAndParameters()
        {
            using var tempFolder = TemporaryDirectory.Create();
            var psScript = @"
param ($value)
echo ""Hello $value"";";
            File.WriteAllText(Path.Combine(tempFolder.DirectoryPath, "myscript.ps1"), psScript);

            return CommandTestBuilder.CreateAsync<RunScriptCommand, Program>()
                                     .WithArrange(context =>
                                                  {
                                                      context.Variables.Add(KnownVariables.Action.Script.ScriptSource, KnownVariableValues.Action.Script.ScriptSource.Package);
                                                      context.Variables.Add(KnownVariables.Action.Script.ScriptFileName, "myscript.ps1");
                                                      context.Variables.Add(KnownVariables.Action.Script.ScriptParameters, "-value abc");
                                                      context.WithFilesToCopy(tempFolder.DirectoryPath);
                                                  })
                                     .WithAssert(result => result.FullLog.Should().Contain("Hello abc"))
                                     .Execute();
        }

        [Test]
        public Task ExecuteWithPackageAndParametersDeployPs1()
        {
            using var tempFolder = TemporaryDirectory.Create();
            var psScript = @"
param ($value)
echo ""Hello $value"";";
            File.WriteAllText(Path.Combine(tempFolder.DirectoryPath, "deploy.ps1"), psScript);

            return CommandTestBuilder.CreateAsync<RunScriptCommand, Program>()
                                     .WithArrange(context =>
                                                  {
                                                      context.Variables.Add(KnownVariables.Action.Script.ScriptSource, KnownVariableValues.Action.Script.ScriptSource.Package);
                                                      context.Variables.Add(KnownVariables.Action.Script.ScriptFileName, "deploy.ps1");
                                                      context.Variables.Add(KnownVariables.Action.Script.ScriptParameters, "-value abc");
                                                      context.WithFilesToCopy(tempFolder.DirectoryPath);
                                                  })
                                     .WithAssert(result => result.FullLog.Should().Contain("Hello abc"))
                                     .Execute();
        }

        [Test]
        public Task EnsureWhenScriptReturnsNonZeroCodeDeploymentFails()
        {
            using var tempFolder = TemporaryDirectory.Create();
            var psScript = "throw 'Error'";
            File.WriteAllText(Path.Combine(tempFolder.DirectoryPath, "myscript.ps1"), psScript);

            return CommandTestBuilder.CreateAsync<RunScriptCommand, Program>()
                                     .WithArrange(context =>
                                                  {
                                                      context.Variables.Add(KnownVariables.Action.Script.ScriptSource, KnownVariableValues.Action.Script.ScriptSource.Inline);
                                                      context.Variables.Add(KnownVariables.Action.Script.Syntax, ScriptSyntax.PowerShell.ToString());
                                                      context.Variables.Add(KnownVariables.Action.Script.ScriptBody, psScript);
                                                  })
                                     .WithAssert(result => result.WasSuccessful.Should().BeFalse())
                                     .Execute(false);
        }

        [Test]
        public Task EnsureWhenInlineScriptFeatureReturnsNonZeroCodeDeploymentFails()
        {
            using var tempFolder = TemporaryDirectory.Create();
            var psScript = "throw 'Error'";
            File.WriteAllText(Path.Combine(tempFolder.DirectoryPath, "myscript.ps1"), psScript);

            return CommandTestBuilder.CreateAsync<RunScriptCommand, Program>()
                                     .WithArrange(context =>
                                                  {
                                                      context.Variables.Add(KnownVariables.Action.Script.ScriptSource, KnownVariableValues.Action.Script.ScriptSource.Package);
                                                      context.Variables.Add("Octopus.Action.Script.ScriptFileName", "myscript.ps1");
                                                      context.WithFilesToCopy(tempFolder.DirectoryPath);
                                                  })
                                     .WithAssert(result => result.WasSuccessful.Should().BeFalse())
                                     .Execute(false);
        }
    }
}