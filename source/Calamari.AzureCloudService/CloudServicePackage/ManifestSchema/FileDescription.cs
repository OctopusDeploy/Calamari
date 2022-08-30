using System;
using System.Xml.Linq;

namespace Calamari.AzureCloudService.CloudServicePackage.ManifestSchema
{
    public class FileDescription
    {
        public static readonly XName ElementName = PackageDefinition.AzureNamespace + "FileDescription";
        static readonly XName DataContentReferenceElementName = PackageDefinition.AzureNamespace + "DataContentReference";
        static readonly XName ReadOnlyElementName = PackageDefinition.AzureNamespace + "ReadOnly";
        static readonly XName CreatedTimeElementName = PackageDefinition.AzureNamespace + "CreatedTimeUtc";
        static readonly XName ModifiedTimeElementName = PackageDefinition.AzureNamespace + "ModifiedTimeUtc";

        public FileDescription()
        { }

        public FileDescription(XElement element)
        {
            DataContentReference = element.Element(DataContentReferenceElementName).Value;
            ReadOnly = Boolean.Parse(element.Element(ReadOnlyElementName).Value);
            Created = DateTime.Parse(element.Element(CreatedTimeElementName).Value);
            Modified = DateTime.Parse(element.Element(ModifiedTimeElementName).Value);
        }

        public string DataContentReference { get; set; }
        public bool ReadOnly { get; set; }
        public DateTime Created { get; set; }
        public DateTime Modified { get; set; }

        public XElement ToXml()
        {
            return new XElement(ElementName,
                new XElement(DataContentReferenceElementName, DataContentReference),
                new XElement(CreatedTimeElementName, Created),
                new XElement(ModifiedTimeElementName, Modified),
                new XElement(ReadOnlyElementName, ReadOnly)
                );
        }
    }
}