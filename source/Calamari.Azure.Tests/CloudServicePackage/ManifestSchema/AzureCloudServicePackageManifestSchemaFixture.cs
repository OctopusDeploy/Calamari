using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Calamari.Azure.Integration.CloudServicePackage.ManifestSchema;
using Calamari.Util;
using NUnit.Framework;

namespace Calamari.Azure.Tests.CloudServicePackage.ManifestSchema
{
    [TestFixture]
    public class AzureCloudServicePackageManifestSchemaFixture
    {
        [Test]
        public void ShouldProduceValidManifestXml()
        {
            var manifest = new PackageDefinition {MetaData = {AzureVersion = "2.6"}};
            manifest.Contents.Add(
                new ContentDefinition
                {
                    Name = "ServiceDefinition/ServiceDefinition.csdef",
                    Description = new ContentDescription
                    {
                        LengthInBytes = 688,
                        HashAlgorithm = IntegrityCheckHashAlgorithm.Sha256,
                        Hash = "NMkHuVQbi+g+ny+kvjiunBgt8rLQT8Jd9FGJvWsRtZE=",
                        DataStorePath = new Uri("ServiceDefinition/ServiceDefinition.csdef", UriKind.Relative)
                    }
                });
            manifest.Contents.Add(
            new ContentDefinition
            {
                Name = "LocalContent/ab3574e8f52d4c939aa6ff065e8b7cfd",
                Description = new ContentDescription
                {
                    LengthInBytes = 19469,
                    HashAlgorithm = IntegrityCheckHashAlgorithm.Sha256,
                    Hash = "UK4lyWPyRI6I8WNzlVP2dtiFZj+kjMIjyGieq5b4TiE=", 
                    DataStorePath = new Uri("LocalContent/ab3574e8f52d4c939aa6ff065e8b7cfd", UriKind.Relative)
                } 
            });

            var workerRoleLayout = new LayoutDefinition {Name = "Roles/WorkerRole1"};
            workerRoleLayout.FileDefinitions.Add(new FileDefinition
            {
                FilePath = "\\Cloud.uar.csman",
                Description =
                    new FileDescription
                    {
                        DataContentReference = "LocalContent/ab3574e8f52d4c939aa6ff065e8b7cfd",
                        Created = DateTime.UtcNow,
                        Modified = DateTime.UtcNow
                    }
            });
            manifest.Layouts.Add(workerRoleLayout);

            var manifestXml = new XDocument(new XDeclaration("1.0", "utf-8", "yes"));
            manifestXml.Add(manifest.ToXml());

            var schemaSet = new XmlSchemaSet();
            schemaSet.Add(null, XmlReader.Create(new StringReader(GetPackageManifestSchema()), XmlUtils.DtdSafeReaderSettings));
            manifestXml.Validate(schemaSet, (o, e) =>
            {
                Assert.Fail("Xml failed to validate: " + e.Message);
            });
        }

        public static string GetPackageManifestSchema()
        {
            using (var stream = typeof (AzureCloudServicePackageManifestSchemaFixture)
                .Assembly.GetManifestResourceStream(
                    "Calamari.Tests.Fixtures.Azure.CloudServicePackage.ManifestSchema.PackageDefinition.xsd"))
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }
    }
}