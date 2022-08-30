﻿using System.Xml.Linq;

 namespace Calamari.AzureCloudService.CloudServicePackage.ManifestSchema
{
    public class ContentDefinition
    {
        public static readonly XName ElementName = PackageDefinition.AzureNamespace + "ContentDefinition";
        static readonly XName NameElementName = PackageDefinition.AzureNamespace + "Name";

        public ContentDefinition()
        {
        }

        public ContentDefinition(XElement element)
        {
            Name = element.Element(NameElementName).Value;
            Description = new ContentDescription(element.Element(ContentDescription.ElementName));
        }

        public string Name { get; set; }
        public ContentDescription Description { get; set; }

        public XElement ToXml()
        {
            return new XElement(ElementName, 
                new XElement(NameElementName, Name), Description.ToXml());
        }
    }
}