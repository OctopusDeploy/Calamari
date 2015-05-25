using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Calamari.Integration.Azure.CloudServicePackage.ManifestSchema
{
    public class PackageDefinition
    {
        public static readonly XNamespace AzureNamespace = "http://schemas.microsoft.com/windowsazure";
        public static readonly XName ElementName = AzureNamespace + "PackageDefinition";
        private static readonly XName PackageContentsElementName = AzureNamespace + "PackageContents";
        private static readonly XName PackageLayoutsElementName = AzureNamespace + "PackageLayouts";

        public PackageDefinition()
        {
            MetaData = new PackageMetaData();
            Layouts = new List<LayoutDefinition>();
            Contents = new List<ContentDefinition>();
        }

        public PackageDefinition(XElement element)
        {
            MetaData = new PackageMetaData(element.Element(PackageMetaData.ElementName));

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

        public PackageMetaData MetaData { get; private set; }

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

        public const string Schema = @"<?xml version=""1.0"" encoding=""utf-8""?>
<xs:schema xmlns:i=""http://www.w3.org/2001/XMLSchema-instance""
           version=""1.0""
           elementFormDefault=""qualified"" 
           targetNamespace=""http://schemas.microsoft.com/windowsazure"" 
           xmlns=""http://schemas.microsoft.com/windowsazure""
           xmlns:xs=""http://www.w3.org/2001/XMLSchema"">

  <xs:simpleType name=""relativeUri"">
    <xs:restriction base=""xs:anyURI""></xs:restriction>
  </xs:simpleType>
  
  <xs:simpleType name=""absoluteUri"">
    <xs:restriction base=""xs:anyURI""></xs:restriction>
  </xs:simpleType>
  
  <xs:simpleType name=""relativeFilePath"">
    <xs:restriction base=""xs:string""></xs:restriction>
  </xs:simpleType>
  
  <xs:element name=""PackageDefinition"" type=""PackageDefintionElement""/>
  
  <xs:complexType name=""PackageDefintionElement"">
    <xs:sequence>
      <xs:element name=""PackageMetaData"" type=""PackageMetaDataElement"">
        <xs:key name=""PackageMetaDataKey"">
          <xs:selector xpath=""KeyValuePair""/>
          <xs:field xpath=""Key""/>
        </xs:key> 
      </xs:element>

      <xs:element name=""PackageContents"" type=""PackageContentsElement"">
        <xs:key name=""PackageContentsKey"">
          <xs:selector xpath=""ContentDefintion""/>
          <xs:field xpath=""Name""/>
        </xs:key>
      </xs:element>
      
      <xs:element name=""PackageLayouts"" type=""PackageLayoutsElement"">
        <xs:key name=""PackageLayoutsKey"">
          <xs:selector xpath=""LayoutDefintion""/>
          <xs:field xpath=""Name""/>
        </xs:key>
      </xs:element>
    </xs:sequence>
  </xs:complexType>
  
  <xs:complexType name=""PackageMetaDataElement"">
    <xs:sequence>
      <xs:element name=""KeyValuePair"" type=""KeyValuePairElement"" minOccurs=""0"" maxOccurs=""unbounded""/>
    </xs:sequence>
  </xs:complexType>
  
  <xs:complexType name=""KeyValuePairElement"">
    <xs:sequence>
      <xs:element name=""Key"" type=""absoluteUri"" />
      <xs:element name=""Value"" type=""xs:string"" />
    </xs:sequence>
  </xs:complexType>

  <xs:complexType name=""PackageContentsElement"">
    <xs:sequence>
      <xs:element  name=""ContentDefinition"" type=""ContentDefintionElement"" minOccurs=""0"" maxOccurs=""unbounded""/>
    </xs:sequence>
  </xs:complexType>

  <xs:complexType name=""ContentDefintionElement"">
    <xs:sequence>
      <xs:element name=""Name"" type=""relativeUri"" />
      <xs:element name=""ContentDescription"" type=""ContentDescriptionElement""/>
    </xs:sequence>
  </xs:complexType>
  
  <xs:complexType name=""ContentDescriptionElement"">
    <xs:sequence>
      <xs:element name=""LengthInBytes"" type=""xs:unsignedLong"" />
      <xs:element name=""IntegrityCheckHashAlgortihm"" type=""xs:string"" />
      <xs:element name=""IntegrityCheckHash"" nillable=""true"" type=""xs:base64Binary""/>
      <xs:element name=""DataStorePath"" type=""relativeUri"" />
    </xs:sequence>
  </xs:complexType>

  <xs:complexType name=""PackageLayoutsElement"">
    <xs:sequence>
      <xs:element name=""LayoutDefinition"" type=""LayoutDefintionElement"" minOccurs=""0"" maxOccurs=""unbounded"" />
    </xs:sequence>
  </xs:complexType>

  <xs:complexType name=""LayoutDefintionElement"">
    <xs:sequence>
      <xs:element name=""Name"" type=""relativeUri"" />
      <xs:element name=""LayoutDescription"" type=""LayountDescriptionElement"">
        <xs:key name=""LayountDescriptionKey"">
          <xs:selector xpath=""FileDefinition""/>
          <xs:field xpath=""FilePath""/>
        </xs:key>
      </xs:element>
    </xs:sequence>
  </xs:complexType>

  <xs:complexType name=""LayountDescriptionElement"">
    <xs:sequence>
      <xs:element name=""FileDefinition"" type=""FileDefinitionElement"" minOccurs=""0""  maxOccurs=""unbounded"">
<!--
        <xs:keyref name=""FileDefintionContentReference"" refer=""PackageContentsKey"">
          <xs:selector xpath=""FileDescription""/>
          <xs:field xpath=""DataContentReference""/>
        </xs:keyref>
-->
      </xs:element>
    </xs:sequence>
  </xs:complexType>

  <xs:complexType name=""FileDefinitionElement"">
    <xs:sequence>
      <xs:element name=""FilePath"" type=""relativeFilePath""/>
      <xs:element name=""FileDescription"" type=""FileDescriptionElement""/>
    </xs:sequence>
  </xs:complexType>

  <xs:complexType name=""FileDescriptionElement"">
    <xs:sequence>
      <xs:element name=""DataContentReference"" type=""relativeUri"" />
      <xs:element name=""CreatedTimeUtc"" type=""xs:dateTime"" />
      <xs:element name=""ModifiedTimeUtc"" type=""xs:dateTime"" />
      <xs:element name=""ReadOnly"" type=""xs:boolean"" />
    </xs:sequence>
  </xs:complexType>  

</xs:schema>
";
    }
}