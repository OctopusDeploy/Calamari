using Amazon.ECS.Model;
using Calamari.Aws.Integration.Ecs.Update;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.AWS.Ecs.Update;

[TestFixture]
public class TaskDefinitionDifferTests
{
    [Test]
    public void Diff_IdenticalTaskDefinitions_ReturnsNoChangesMessage()
    {
        var a = new TaskDefinition { Family = "f" };
        var b = new TaskDefinition { Family = "f" };

        TaskDefinitionDiffer.Diff(a, b)
            .Should().Be("No changes were detected in the task definition.");
    }
}
