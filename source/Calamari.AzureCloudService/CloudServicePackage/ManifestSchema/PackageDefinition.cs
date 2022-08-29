using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Calamari.AzureCloudService.CloudServicePackage.ManifestSchema
{
    public class PackageDefinition
    {
        public static readonly XNamespace AzureNamespace = "http://schemas.microsoft.com/windowsazure";
        public static readonly XName ElementName = AzureNamespace + "PackageDefinition";
        static readonly XName PackageContentsElementName = AzureNamespace + "PackageContents";
        static readonly XName PackageLayoutsElementName = AzureNamespace + "PackageLayouts";

        public PackageDefinition()
        {
            MetaData = new AzurePackageMetadata();
            Layouts = new List<LayoutDefinition>();
            Contents = new List<ContentDefinition>();
        }

        public PackageDefinition(XElement element)
        {
            MetaData = new AzurePackageMetadata(element.Element(AzurePackageMetadata.ElementName));

            Contents = element
                .Element(PackageContentsElementName)
                .Elements(ContentDefinition.ElementName)
                .Select(x => new ContentDefinition(x))
                .ToList();

            Layouts = element
                .Element(PackageLayoutsElementName)
                .Elements(LayoutDefinition.ElementName)
                .Select(x => new LayoutDefinition(x))
                .ToList();
        }

        public AzurePackageMetadata MetaData { get; private set; }

        public ICollection<ContentDefinition> Contents { get; private set; }

        public ICollection<LayoutDefinition> Layouts { get; private set; }

        public ContentDefinition GetContentDefinition(string name)
        {
            return Contents.Single(x => x.Name == name);
        }

        public XElement ToXml()
        {
           return new XElement(ElementName,
               MetaData.ToXml(), 
               new XElement(PackageContentsElementName, Contents.Select(x => x.ToXml())),
               new XElement(PackageLayoutsElementName, Layouts.Select(x => x.ToXml()))
               ); 
        }
    }
}