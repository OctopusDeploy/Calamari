using System.IO;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.NamingIsHard.Commands;
using Calamari.Tests.Shared;
using NUnit.Framework;

namespace Calamari.NamingIsHard.Tests
{
    [TestFixture]
    public class MyCommandFixtures
    {
        [Test]
        public Task ExecuteCommand()
        {
            // I can create content to be copied, this bypasses the extracting package
            using var tempPath = TemporaryDirectory.Create();
            File.WriteAllText(Path.Combine(tempPath.DirectoryPath, "PreDeploy.ps1"), "echo \"Hello from PreDeploy\"");

            return CommandTestBuilder.CreateAsync<MyCommand, Program>()
                .WithArrange(context =>
                {
                    context.Variables.Add("MyVariable", "MyValue");
                    context.WithFilesToCopy(tempPath.DirectoryPath);
                })
                .Execute();
        }
    }
}