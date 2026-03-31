using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests;

public class ProgramTest
{
    [Test]
    public void CanExecuteCleanPackages()
    {
        var uut = Program.Main(["clean-packages"]);
        uut.Should().Be(0);
    }
}