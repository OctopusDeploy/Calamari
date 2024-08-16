using System.Collections.Generic;
using System.Text;
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
    public class KeyValuesValuesFileWriterFixture
    {
        RunningDeployment deployment;
        ICalamariFileSystem fileSystem;

        [SetUp]
        public void SetUp()
        {
            deployment = new RunningDeployment(new CalamariVariables());
            fileSystem = Substitute.For<ICalamariFileSystem>();
        }
        
        [Test]
        public void WriteToFile_NoKeyValues_ReturnsNullAndNothingIsWritten()
        {
            //Act
            var result = KeyValuesValuesFileWriter.WriteToFile(deployment, fileSystem, new Dictionary<string, object>());
            
            //Assert
            result.Should().BeNull();
            
            fileSystem.Received(0).WriteAllText(Arg.Any<string>(), Arg.Any<string>());
        }
        
        [Test]
        public void WriteToFile_SomeKeyValues_WritesFileAndReturnsFilename()
        {
            string writtenFilename = null;
            fileSystem.When(fs => fs.WriteAllText(Arg.Any<string>(), Arg.Any<string>()))
                      .Do(ci =>
                          {
                              writtenFilename = ci.ArgAt<string>(0);
                          });
            
            // Act
            var result = KeyValuesValuesFileWriter.WriteToFile(deployment, fileSystem, new Dictionary<string, object>
            {
                ["key 1"] = "value 1"
            });
            
            //Assert
            result.Should()
                  .NotBeNull()
                  .And
                  .Be(writtenFilename);
            
            fileSystem.Received(1).WriteAllText(Arg.Any<string>(), Arg.Any<string>());
        }
        
        [Test]
        public void WriteToFile_SomeKeyValuesWithIndex_WritesFileAndReturnsFilenameWithIndex()
        {
            string writtenFilename = null;
            fileSystem.When(fs => fs.WriteAllText(Arg.Any<string>(), Arg.Any<string>()))
                      .Do(ci =>
                          {
                              writtenFilename = ci.ArgAt<string>(0);
                          });
            
            // Act
            var result = KeyValuesValuesFileWriter.WriteToFile(deployment, fileSystem, new Dictionary<string, object>
            {
                ["key 1"] = "value 1"
            }, 100);
            
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