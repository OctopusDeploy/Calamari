using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.XPath;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Wmhelp.XPath2;

namespace Calamari.Common.Features.StructuredVariables
{
    public class XmlFormatVariableReplacer : IFileFormatVariableReplacer
    {
        readonly ICalamariFileSystem fileSystem;
        readonly ILog log;

        public XmlFormatVariableReplacer(ICalamariFileSystem fileSystem, ILog log)
        {
            this.fileSystem = fileSystem;
            this.log = log;
        }
        
        public string FileFormatName => StructuredConfigVariablesFileFormats.Xml;
        
        public bool IsBestReplacerForFileName(string fileName)
        {
            return fileName.EndsWith(".xml", StringComparison.InvariantCultureIgnoreCase);
        }

        public void ModifyFile(string filePath, IVariables variables)
        {
            var fileContents = fileSystem.ReadFile(filePath);
            
            var doc = new XmlDocument();
            try
            {
                doc.LoadXml(fileContents);
            }
            catch (XmlException e)
            {
                throw new StructuredConfigFileParseException(e.Message, e);
            }

            var nsManager = BuildNsManagerFromDocument(doc);
            var navigator = doc.CreateNavigator();
            
            foreach (var variable in variables)
            {
                if (IsValidXPath(variable.Key, nsManager))
                {
                    var xPathExpression = variable.Key;
                    var selectedNodes = navigator.XPath2SelectNodes(xPathExpression, nsManager);

                    foreach (XPathNavigator selectedNode in selectedNodes)
                    {
                        switch (selectedNode.UnderlyingObject)
                        {
                            case XmlText text:
                                text.Data = variable.Value;
                                break;

                            case XmlAttribute attribute:
                                attribute.Value = variable.Value;
                                break;

                            case XmlComment comment:
                                comment.Data = variable.Value;
                                break;

                            case XmlElement element:
                                if (element.ChildNodes.Count == 1 && element.ChildNodes[0].NodeType == XmlNodeType.CDATA)
                                {
                                    // Try to preserve CDatas in the output.
                                    element.ChildNodes[0].Value = variable.Value;
                                }
                                else if (ContainsElements(element))
                                {
                                    TrySetInnerXml(element, xPathExpression, variable.Value);
                                }
                                else
                                {
                                    element.InnerText = variable.Value;
                                }
                                break;

                            case XmlCharacterData cData:
                                cData.Data = variable.Value;
                                break;

                            case XmlProcessingInstruction processingInstruction:
                                processingInstruction.Data = variable.Value;
                                break;
                            
                            case XmlNode node:
                                log.Warn($"Node of type {node.NodeType} not supported");
                                break;

                            default:
                                // TODO: consider whether to silently ignore
                                throw new Exception($"Can't handle type {selectedNode.GetType().FullName}");
                        }
                    }
                }
            }

            using (var output = new StringWriter())
            using (var writer = new XmlTextWriter(output))
            {
                writer.Formatting = Formatting.Indented;
                
                doc.WriteTo(writer);
                fileSystem.OverwriteFile(filePath, output.ToString());
            }
        }

        void TrySetInnerXml(XmlElement element, string xpathExpression, string variableValue)
        {
            var previousInnerXml = element.InnerXml;
            
            try
            {
                element.InnerXml = variableValue;
            }
            catch (XmlException e)
            {
                element.InnerXml = previousInnerXml;
                log.Warn($"Could not set the value of the XML element at XPath "
                         + $"'{xpathExpression}' to '{variableValue}'. Expected "
                         + $"a valid XML fragment. Skipping replacement of this "
                         + $"element.");
            }
        }

        class NamespaceDeclaration
        {
            public string Prefix { get; }
            
            public string NamespaceUri { get; }

            public NamespaceDeclaration(string prefix, string namespaceUri)
            {
                Prefix = prefix;
                NamespaceUri = namespaceUri;
            }
        }

        bool ContainsElements(XmlElement element)
        {
            return element.ChildNodes
                          .Cast<XmlNode>()
                          .Any(n => n.NodeType == XmlNodeType.Element);
        }
        
        XmlNamespaceManager BuildNsManagerFromDocument(XmlDocument doc)
        {
            var nsManager = new XmlNamespaceManager(doc.NameTable);
            var namespaces = GetDeclaredNamespaceUris(doc);
            
            foreach (var ns in namespaces)
            {
                var existing = nsManager.LookupNamespace(ns.Prefix);
                if (existing != null && existing != ns.NamespaceUri)
                {
                    var msg = $"The namespace '{ns.NamespaceUri}' could not be mapped to the '{ns.Prefix}' "
                              + $"prefix, as another namespace '{existing}' is already mapped to that "
                              + $"prefix. XPath selectors using this prefix may not return the expected nodes. "
                              + $"You can avoid this by ensuring all namespaces in your document have unique "
                              + $"prefixes.";
                    
                    log.Warn(msg);
                }
                else
                {
                    nsManager.AddNamespace(ns.Prefix, ns.NamespaceUri);
                }
            }
            
            return nsManager;
        }

        IEnumerable<NamespaceDeclaration> GetDeclaredNamespaceUris(XmlNode node)
        {
            if (node.Attributes != null)
            {
                foreach (var attribute in node.Attributes)
                {
                    if (attribute is XmlAttribute attr)
                    {
                        if (attr.NamespaceURI == "http://www.w3.org/2000/xmlns/")
                        {
                            if (attr.LocalName != "xmlns")
                            {
                                yield return new NamespaceDeclaration(attr.LocalName, attr.Value);
                            }
                        }
                    }
                }
            }
            
            foreach (var child in node.ChildNodes)
            {
                if (child is XmlNode childNode)
                {
                    var declaredNamespacesInChild = GetDeclaredNamespaceUris(childNode);
                    foreach (var declaredNamespace in declaredNamespacesInChild)
                    {
                        yield return declaredNamespace;
                    }
                }
            }
        }

        bool IsValidXPath(string xPath, IXmlNamespaceResolver nsResolver)
        {
            try
            {
                XPath2Expression.Compile(xPath, nsResolver);
                return true;
            }
            catch (XPath2Exception)
            {
                return false;
            }
        }
    }
}