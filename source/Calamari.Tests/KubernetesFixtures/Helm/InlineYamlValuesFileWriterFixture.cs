using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.Helm;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.Helm
{
    [TestFixture]
    public class InlineYamlValuesFileWriterFixture
    {
        RunningDeployment deployment;
        ICalamariFileSystem fileSystem;

        [SetUp]
        public void SetUp()
        {
            deployment = new RunningDeployment(new CalamariVariables());
            fileSystem = Substitute.For<ICalamariFileSystem>();
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("                ")]
        public void WriteToFile_YamlIsNullOrWriteSpace_ReturnsNullAndNoFileIsWritten(string yaml)
        {
            // Act
            var result = InlineYamlValuesFileWriter.WriteToFile(deployment, fileSystem, yaml);
            
            // Assert
            result.Should().BeNull();
            
            fileSystem.Received(0).WriteAllText(Arg.Any<string>(), Arg.Any<string>());
        }
        
        [Test]
        public void WriteToFile_SomeYaml_WritesFileAndReturnsFilename()
        {
            string writtenFilename = null;
            fileSystem.When(fs => fs.WriteAllText(Arg.Any<string>(), Arg.Any<string>()))
                      .Do(ci =>
                          {
                              writtenFilename = ci.ArgAt<string>(0);
                          });
            
            // Act
            var result = InlineYamlValuesFileWriter.WriteToFile(deployment, fileSystem, "key: value");
            
            //Assert
            result.Should()
                  .NotBeNull()
                  .And
                  .Be(writtenFilename);
            
            fileSystem.Received(1).WriteAllText(Arg.Any<string>(), Arg.Any<string>());
        }
        
        [Test]
        public void WriteToFile_SomeYamlWithIndex_GetsWritesFileAndReturnsFilenameWithIndex()
        {
            string writtenFilename = null;
            fileSystem.When(fs => fs.WriteAllText(Arg.Any<string>(), Arg.Any<string>()))
                      .Do(ci =>
                          {
                              writtenFilename = ci.ArgAt<string>(0);
                          });
            
            // Act
            var result = InlineYamlValuesFileWriter.WriteToFile(deployment, fileSystem, "key: value", 100);
            
            //Assert
            result.Should()
                  .NotBeNull()
                  .And
                  .Be(writtenFilename);

            result.Should().EndWith("-100.yaml");
            
            fileSystem.Received(1).WriteAllText(Arg.Any<string>(), Arg.Any<string>());
        }
    }
}