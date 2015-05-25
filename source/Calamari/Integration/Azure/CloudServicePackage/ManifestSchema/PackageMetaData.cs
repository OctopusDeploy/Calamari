using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Calamari.Integration.Azure.CloudServicePackage.ManifestSchema
{
    public class PackageMetaData
    {
        public static readonly XName ElementName = PackageDefinition.AzureNamespace + "PackageMetaData";
        private static readonly XName KeyValuePairElementName = PackageDefinition.AzureNamespace + "KeyValuePair";
        private static readonly XName KeyElementName = PackageDefinition.AzureNamespace + "Key";
        private static readonly XName ValueElementName = PackageDefinition.AzureNamespace + "Value";

        public PackageMetaData()
        {
            Data = new List<KeyValuePair<string, string>>();
        }

        public PackageMetaData(XElement element)
        {
            Data = element.Elements(KeyValuePairElementName)
                .Select(x => new KeyValuePair<string, string>(
                    x.Element(KeyElementName).Value, x.Element(ValueElementName).Value
                    ))
                .ToList();

            AzureVersion =
                Data.Where(kv => kv.Key == "http://schemas.microsoft.com/windowsazure/ProductVersion/")
                    .Select(kv => kv.Value)
                    .SingleOrDefault();
        }

        public ICollection<KeyValuePair<string, string>> Data { get; private set; }

        public string AzureVersion { get; set; }

        public XElement ToXml()
        {
            return new XElement(ElementName,
                Data.Select(kv => new XElement(KeyValuePairElementName,
                    new XElement(KeyElementName, kv.Key), new XElement(ValueElementName, kv.Value))));
        }
    }
}