using System.IO;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using IoC;
using Octopus.Server.Extensibility.Metadata;
using Sashimi.Server.Contracts.CloudTemplates;
using Sashimi.Tests.Shared.Extensions;

namespace Sashimi.Aws.Tests.Templates
{
    public class CloudFormationCloudTemplateHandlerTestBase
    {
        protected ICloudTemplateHandler templateParser = null;

        protected void MetadataShouldContainCorrectNumberOfTypes(Metadata metadata, int count)
        {
            metadata.Should().NotBeNull();
            metadata.Types.Should().NotBeEmpty();
            metadata.Types.Count.Should().Be(count);
        }

        protected void MetadataTypeShouldContainCorrectNumberOfProperties(TypeMetadata typeMetadata, int count)
        {
            typeMetadata.Should().NotBeNull();
            var constraint = count == 0 ? typeMetadata.Properties.Should().BeEmpty() : typeMetadata.Properties.Should().NotBeEmpty();
            typeMetadata.Properties.Count().Should().Be(count);
        }

        protected string LoadTemplate(string fileName)
        {
            var templatesPath = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().FullLocalPath()) ?? string.Empty,
                Path.Combine("Templates","TemplateSamples"));

            return File.ReadAllText(Path.Combine(templatesPath, fileName));
        }
    }
}