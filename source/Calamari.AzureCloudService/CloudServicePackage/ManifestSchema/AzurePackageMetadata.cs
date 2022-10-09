using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Calamari.AzureCloudService.CloudServicePackage.ManifestSchema
{
    public class AzurePackageMetadata
    {
        public static readonly XName ElementName = PackageDefinition.AzureNamespace + "PackageMetaData";
        static readonly XName KeyValuePairElementName = PackageDefinition.AzureNamespace + "KeyValuePair";
        static readonly XName KeyElementName = PackageDefinition.AzureNamespace + "Key";
        static readonly XName ValueElementName = PackageDefinition.AzureNamespace + "Value";

        const string AzureVersionKey = "http://schemas.microsoft.com/windowsazure/ProductVersion/";

        public AzurePackageMetadata()
        {
            Data = new Dictionary<string, string>();
        }

        public AzurePackageMetadata(XElement element)
        {
            Data = element.Elements(KeyValuePairElementName)
                .ToDictionary(x => x.Element(KeyElementName).Value, x => x.Element(ValueElementName).Value);
        }

        public IDictionary<string, string> Data { get; private set; }

        public string AzureVersion
        {
            get
            {
                string version;
                return Data.TryGetValue(AzureVersionKey, out version)
                    ? version
                    : null;
            }
            set { Data[AzureVersionKey] = value; }
        }

        public XElement ToXml()
        {
            return new XElement(ElementName,
                Data.Select(kv => new XElement(KeyValuePairElementName,
                    new XElement(KeyElementName, kv.Key), new XElement(ValueElementName, kv.Value))).ToArray());
        }
    }
}