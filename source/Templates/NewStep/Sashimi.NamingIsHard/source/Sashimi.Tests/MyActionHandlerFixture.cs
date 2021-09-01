using System;
using System.IO;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.NamingIsHard;
using FluentAssertions;
using NUnit.Framework;
using Sashimi.Tests.Shared.Server;

namespace Sashimi.NamingIsHard.Tests
{
    [TestFixture]
    public class MyActionHandlerFixture
    {
        [Test]
        public void Test1()
        {
            // I can create content to be copied, this bypasses the extracting package
            using var tempPath = TemporaryDirectory.Create();
            File.WriteAllText(Path.Combine(tempPath.DirectoryPath, "PreDeploy.ps1"), "echo \"Hello $Name\"");

            ActionHandlerTestBuilder.CreateAsync<MyActionHandler, Program>()
                                    .WithArrange(context =>
                                                 {
                                                     context.Variables.Add("Name", "World");
                                                     context.WithFilesToCopy(tempPath.DirectoryPath);
                                                 })
                                    .WithAssert(result => result.FullLog.Should().Contain("Hello World"));
        }
    }
}