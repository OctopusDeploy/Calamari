using System;
using System.IO;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.NamingIsHard.Commands;
using Calamari.Tests.Shared;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.NamingIsHard.Tests
{
    [TestFixture]
    public class MyCommandFixtures
    {
        /*
         * This test shows how to test a Calamari command directly without going through the Server handler.
         * Majority of time we prefer to write the same test in Sashimi.Test, it ends-up testing more code and also behaves the same as the Server calling it.
         * However sometimes that is not possible, example, in Sashimi Azure CloudServices module, the Calamari project references very old nuget packages that
         * do not have netstandard targets and hence is not possible to load those assemblies in Sashimi because Sashimi target netstandard only.
         * So the tl;dr; only write Calamari command tests when you cannot test them via Sashimi.
         */
        [Test]
        public Task ExecuteCommand()
        {
            // I can create content to be copied, this bypasses the extracting package
            using var tempPath = TemporaryDirectory.Create();
            File.WriteAllText(Path.Combine(tempPath.DirectoryPath, "PreDeploy.ps1"), "echo \"Hello $Name\"");

            return CommandTestBuilder.CreateAsync<MyCommand, Program>()
                                     .WithArrange(context =>
                                                  {
                                                      context.Variables.Add("Name", "World");
                                                      context.WithFilesToCopy(tempPath.DirectoryPath);
                                                  })
                                     .WithAssert(result => result.FullLog.Should().Contain("Hello World"))
                                     .Execute();
        }
    }
}