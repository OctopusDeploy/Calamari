using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.XPath;
using Calamari.Common.Plumbing.Extensions;
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
            var encodingPrecedence = new List<Encoding>(CalamariPhysicalFileSystem.DefaultInputEncodingPrecedence);

            var fileBytes = fileSystem.ReadAllBytes(filePath);
            if (TryGetDeclaredEncoding(fileBytes) is {} declaredEncoding)
                encodingPrecedence.Insert(0, declaredEncoding);

            var fileText = fileSystem.ReadAllText(fileBytes, out var encoding, encodingPrecedence);
            var lineEnding = fileText.GetMostCommonLineEnding();

            var doc = new XmlDocument();
            try
            {
                doc.LoadXml(fileText);
            }
            catch (XmlException e)
            {
                throw new StructuredConfigFileParseException(e.Message, e);
            }

            var nsManager = BuildNsManagerFromDocument(doc);
            var navigator = doc.CreateNavigator();

            var replaced = 0;
            foreach (var variable in variables)
                if (TryGetXPathFromVariableKey(variable.Key, nsManager) is {} xPathExpression)
                {
                    var selectedNodes = navigator.XPath2SelectNodes(xPathExpression, nsManager);
                    var variableValue = variables.Get(variable.Key);
                    if (variableValue == null)
                        continue;

                    foreach (XPathNavigator selectedNode in selectedNodes)
                    {
                        log.Verbose(StructuredConfigMessages.StructureFound(variable.Key));
                        replaced++;

                        switch (selectedNode.UnderlyingObject)
                        {
                            case XmlText text:
                                text.Data = variableValue;
                                break;

                            case XmlAttribute attribute:
                                attribute.Value = variableValue;
                                break;

                            case XmlComment comment:
                                comment.Data = variableValue;
                                break;

                            case XmlElement element:
                                if (element.ChildNodes.Count == 1 && element.ChildNodes[0].NodeType == XmlNodeType.CDATA)
                                    // Try to preserve CDatas in the output.
                                    element.ChildNodes[0].Value = variableValue;
                                else if (ContainsElements(element))
                                    TrySetInnerXml(element, variable.Key, variableValue);
                                else
                                    element.InnerText = variableValue;
                                break;

                            case XmlCharacterData cData:
                                cData.Data = variableValue;
                                break;

                            case XmlProcessingInstruction processingInstruction:
                                processingInstruction.Data = variableValue;
                                break;

                            case XmlNode node:
                                log.Warn($"XML Node of type '{node.NodeType}' is not supported");
                                break;

                            default:
                                log.Warn($"XPath returned an object of type '{selectedNode.GetType().FullName}', which is not supported");
                                break;
                        }
                    }
                }
            if (replaced == 0)
                log.Info(StructuredConfigMessages.NoStructuresFound);

            fileSystem.OverwriteFile(filePath,
                                     textWriter =>
                                     {
                                         var xmlWriterSettings = new XmlWriterSettings
                                         {
                                             Indent = true,
                                             NewLineChars = lineEnding == StringExtensions.LineEnding.Dos ? "\r\n" : "\n",
                                             OmitXmlDeclaration = doc.FirstChild.NodeType != XmlNodeType.XmlDeclaration
                                         };
                                         using (var writer = XmlWriter.Create(textWriter, xmlWriterSettings))
                                         {
                                             doc.Save(writer);
                                             writer.Close();
                                         }
                                     },
                                     encoding);
        }

        void TrySetInnerXml(XmlElement element, string xpathExpression, string variableValue)
        {
            var previousInnerXml = element.InnerXml;

            try
            {
                element.InnerXml = variableValue;
            }
            catch (XmlException)
            {
                element.InnerXml = previousInnerXml;
                log.Warn("Could not set the value of the XML element at XPath "
                         + $"'{xpathExpression}' to '{variableValue}'. Expected "
                         + "a valid XML fragment. Skipping replacement of this "
                         + "element.");
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
            var namespaces = GetNamespacesFromNodeAndDescendants(doc);

            foreach (var ns in namespaces)
            {
                var existing = nsManager.LookupNamespace(ns.Prefix);
                if (existing != null && existing != ns.NamespaceUri)
                {
                    var msg = $"The namespace '{ns.NamespaceUri}' could not be mapped to the '{ns.Prefix}' "
                              + $"prefix, as another namespace '{existing}' is already mapped to that "
                              + "prefix. XPath selectors using this prefix may not return the expected nodes. "
                              + "You can avoid this by ensuring all namespaces in your document have unique "
                              + "prefixes.";

                    log.Warn(msg);
                }
                else
                {
                    nsManager.AddNamespace(ns.Prefix, ns.NamespaceUri);
                }
            }

            return nsManager;
        }

        IEnumerable<NamespaceDeclaration> GetNamespacesFromNodeAndDescendants(XmlNode node)
        {
            if (node.Attributes != null)
                foreach (var namespaceFromAttribute in node.Attributes
                                                           .OfType<XmlAttribute>()
                                                           .Where(attr =>
                                                                      attr.NamespaceURI == "http://www.w3.org/2000/xmlns/"
                                                                      && attr.LocalName != "xmlns")
                                                           .Select(attr => new NamespaceDeclaration(attr.LocalName, attr.Value)))
                    yield return namespaceFromAttribute;

            foreach (var namespaceFromDescendants in node.ChildNodes
                                                         .OfType<XmlNode>()
                                                         .SelectMany(GetNamespacesFromNodeAndDescendants))
                yield return namespaceFromDescendants;
        }

        XPath2Expression? TryGetXPathFromVariableKey(string variableKey, IXmlNamespaceResolver nsResolver)
        {
            // Prevent 'Octopus*' and other unintended variables being recognized as XPath expressions selecting the document node
            if (!variableKey.Contains("/")
                && !variableKey.Contains(":"))
                return null;

            try
            {
                return XPath2Expression.Compile(variableKey, nsResolver);
            }
            catch (XPath2Exception)
            {
                return null;
            }
        }

        public static Encoding? TryGetDeclaredEncoding(byte[] bytes)
        {
            try
            {
                using (var stream = new MemoryStream(bytes))
                using (var xmlReader = XmlReader.Create(stream))
                {
                    if (xmlReader.Read() && xmlReader.NodeType == XmlNodeType.XmlDeclaration)
                        return Encoding.GetEncoding(xmlReader.GetAttribute("encoding"), EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
                    return null;
                }
            }
            catch
            {
                return null;
            }
        }

        class NamespaceDeclaration
        {
            public NamespaceDeclaration(string prefix, string namespaceUri)
            {
                Prefix = prefix;
                NamespaceUri = namespaceUri;
            }

            public string Prefix { get; }

            public string NamespaceUri { get; }
        }
    }
}