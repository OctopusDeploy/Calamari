using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing.Helpers;
using Calamari.Tests.Fixtures.Integration.FileSystem;
using FluentAssertions;
using Google.Protobuf.Reflection;
using NUnit.Framework;
using NUnit.Framework.Internal;

namespace Calamari.Tests;

[TestFixture]
public class CommitToGitCommandTest
{
    readonly ILog log = new InMemoryLog();
    readonly ICalamariFileSystem fileSystem = TestCalamariPhysicalFileSystem.GetPhysicalFileSystem();
    readonly string variablePassword = "password";
    readonly string variableFileName = "variables.json";
    string executionDirectory;

    [SetUp]
    public void setUp()
    {
        executionDirectory = fileSystem.CreateTemporaryDirectory();
    }
    
    
    [Test]
    [Category("Nix")]
    public void CommitToGitRunsScriptWithNoDependencies()
    {
        var variables = new CalamariExecutionVariableCollection
        {
            new CalamariExecutionVariable(ScriptVariables.ScriptBody, "exit 0", false),
            new CalamariExecutionVariable(ScriptVariables.Syntax, ScriptSyntax.Bash.ToString(), false),
        };
        var absPathToVariables = Path.Combine(executionDirectory, variableFileName);
        File.WriteAllBytes(absPathToVariables, AesEncryption.ForServerVariables(variablePassword).Encrypt(variables.ToJsonString()));

        var result = Program.Main(["commit-to-git", "--variables", $"{absPathToVariables}", "--variablesPassword", $"{variablePassword}"]);
        result.Should().Be(0);
    }

    [Test]
    [Category("Nix")]
    public void CommitToGitRunsScriptWithPackageDependency()
    {
        const string packageReferenceName = "my-scripts";
        var zipPath = Path.Combine(executionDirectory, $"{packageReferenceName}.1.0.0.zip");
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("helper.sh");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("exit 0");
        }

        var variables = new CalamariExecutionVariableCollection
        {
            new CalamariExecutionVariable(ScriptVariables.ScriptBody, $"bash {packageReferenceName}/helper.sh", false),
            new CalamariExecutionVariable(ScriptVariables.Syntax, ScriptSyntax.Bash.ToString(), false),
            new CalamariExecutionVariable(PackageVariables.IndexedPackageId(packageReferenceName), packageReferenceName, false),
            new CalamariExecutionVariable(PackageVariables.IndexedOriginalPath(packageReferenceName), zipPath, false),
        };
        var absPathToVariables = Path.Combine(executionDirectory, variableFileName);
        File.WriteAllBytes(absPathToVariables, AesEncryption.ForServerVariables(variablePassword).Encrypt(variables.ToJsonString()));

        var result = Program.Main(["commit-to-git", "--variables", absPathToVariables, "--variablesPassword", variablePassword]);
        result.Should().Be(0);
    }

    [Test]
    public void CommitToGitCanBeCreated()
    {
        var firstVariableCollection = new CalamariExecutionVariableCollection
        {
            new CalamariExecutionVariable("firstVariableName", "firstVariableValue", false)
        };
        var absPathToVariables = Path.Combine(executionDirectory, variableFileName);
        
        File.WriteAllBytes(absPathToVariables, AesEncryption.ForServerVariables(variablePassword).Encrypt(firstVariableCollection.ToJsonString()));
        
        var result = Program.Main(["commit-to-git", $"variables={absPathToVariables}", $"variablesPassword={variablePassword}"]);
        result.Should().Be(0);
    }
}