﻿using System.Xml.Linq;

 namespace Calamari.AzureCloudService.CloudServicePackage.ManifestSchema
{
    public class FileDefinition
    {
        public static readonly XName ElementName = PackageDefinition.AzureNamespace + "FileDefinition";
        static readonly XName FilePathElementName = PackageDefinition.AzureNamespace + "FilePath";

        public FileDefinition()
        {
        }

        public FileDefinition(XElement element)
        {
            FilePath = element.Element(FilePathElementName).Value;
            Description = new FileDescription(element.Element(FileDescription.ElementName));
        }

        public string FilePath { get; set; }
        public FileDescription Description { get; set; }

        public XElement ToXml()
        {
            return new XElement(ElementName,
                new XElement(FilePathElementName, FilePath),
                Description.ToXml());
        }
    }
}