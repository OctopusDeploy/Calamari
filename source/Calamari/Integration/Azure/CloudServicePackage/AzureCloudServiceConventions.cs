using System.IO.Packaging;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Calamari.Integration.Azure.CloudServicePackage.ManifestSchema;

namespace Calamari.Integration.Azure.CloudServicePackage
{
    public static class AzureCloudServiceConventions
    {
        public const string RoleLayoutPrefix = "Roles/";
        public const string CtpFormatPackageDefinitionRelationshipType = "http://schemas.microsoft.com/windowsazure/PackageDefinition/Version/2012/03/15"; 

        public static PackageDefinition ReadPackageManifest(Package package)
        {
            var manifestPart = package.GetPart(
                package.GetRelationshipsByType(CtpFormatPackageDefinitionRelationshipType).Single().TargetUri);

            using (var manifestStream = manifestPart.GetStream())
            using (var xmlReader = XmlReader.Create(manifestStream))
            {
               return new PackageDefinition(XDocument.Load(xmlReader).Root);
            }
        }

        public static bool RoleLayoutFilePathIsModifiable(string filePath)
        {
            return filePath.StartsWith("approot\\") || filePath.StartsWith("sitesroot\\");
        }
    }
}