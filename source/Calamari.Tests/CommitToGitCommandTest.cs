using Calamari.Common.Plumbing.Logging;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests;

[TestFixture]
public class CommitToGitCommandTest
{
    readonly ILog log = new InMemoryLog();
    
    [Test]
    public void CommitToGitCanBeCreated()
    {
        var result = Program.Main(["commit-to-git"]);
        result.Should().Be(0);
    }
}