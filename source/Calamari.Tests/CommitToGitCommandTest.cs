using System.Collections.Generic;
using System.IO;
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