using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Calamari.Common.Commands;
using Calamari.Common.Features.Substitutions;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing.Helpers;
using Calamari.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Substitutions
{
    [TestFixture]
    public class NonSensitiveSubstitutionsFixture : CalamariFixture
    {
        static readonly CalamariPhysicalFileSystem FileSystem = CalamariEnvironment.IsRunningOnWindows ? (CalamariPhysicalFileSystem)new WindowsPhysicalFileSystem() : new NixCalamariPhysicalFileSystem();

        [Test]
        public void WhenVariableIsNotInVariableList_ShouldThrowCommandException()
        {
            // Arrange
            var variables = new NonSensitiveCalamariVariables
            {
                ["ServerEndpoints[FOREXUAT01].Name"] = "forexuat01.local",
                ["ServerEndpoints[FOREXUAT01].Port"] = "1566",
                ["ServerEndpoints[FOREXUAT02].Name"] = "forexuat02.local",
            };

            // Act
            Action action = () =>
                            {
                                _ = PerformTest(GetFixtureResource("Samples", "Servers.json"), variables).text;
                            };

            // Assert
            action.Should()
                  .Throw<CommandException>()
                  .WithMessage("*#{server.Port}*This may be due to missing or sensitive variables.");
        }

        static (string text, Encoding encoding) PerformTest(string sampleFile, INonSensitiveVariables variables, Encoding expectedResultEncoding = null)
        {
            var temp = Path.GetTempFileName();
            using (new TemporaryFile(temp))
            {
                var substituter = new NonSensitiveFileSubstituter(new InMemoryLog(), FileSystem, variables);
                substituter.PerformSubstitution(sampleFile, temp);
                using (var reader = new StreamReader(temp, expectedResultEncoding ?? new UTF8Encoding(false, true), expectedResultEncoding == null))
                {
                    return (reader.ReadToEnd(), reader.CurrentEncoding);
                }
            }
        }
    }
}