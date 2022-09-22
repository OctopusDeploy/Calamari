using System;
using System.Xml.Linq;

namespace Calamari.AzureCloudService.CloudServicePackage.ManifestSchema
{
    public class ContentDescription
    {
        public static readonly XName ElementName = PackageDefinition.AzureNamespace + "ContentDescription";
        static readonly XName LengthInBytesElementName = PackageDefinition.AzureNamespace + "LengthInBytes";
        static readonly XName DataStorePathElementName = PackageDefinition.AzureNamespace + "DataStorePath";
        static readonly XName HashAlgorithmElementName = PackageDefinition.AzureNamespace + "IntegrityCheckHashAlgortihm";
        static readonly XName HashElementName = PackageDefinition.AzureNamespace + "IntegrityCheckHash";

        public ContentDescription()
        {
        }

        public ContentDescription(XElement element)
        {
            LengthInBytes = int.Parse(element.Element(LengthInBytesElementName).Value);
            DataStorePath = new Uri(element.Element(DataStorePathElementName).Value, UriKind.Relative);
            HashAlgorithm = (IntegrityCheckHashAlgorithm)Enum.Parse(typeof(IntegrityCheckHashAlgorithm), element.Element(HashAlgorithmElementName).Value);

            if (HashAlgorithm != IntegrityCheckHashAlgorithm.None)
                Hash = element.Element(HashElementName).Value;
        }

        public int LengthInBytes { get; set; }
        public Uri DataStorePath { get; set; }
        public IntegrityCheckHashAlgorithm HashAlgorithm { get; set; }
        public string Hash { get; set; }

        public XElement ToXml()
        {
            return new XElement(ElementName,
                new XElement(LengthInBytesElementName, LengthInBytes.ToString()),
                new XElement(HashAlgorithmElementName, HashAlgorithm.ToString()),
                HashAlgorithm == IntegrityCheckHashAlgorithm.None
                    ? new XElement(HashElementName, new XAttribute("{http://www.w3.org/2001/XMLSchema-instance}nil", true))
                    : new XElement(HashElementName, Hash),
                new XElement(DataStorePathElementName, DataStorePath)
                );
        }
    }
}