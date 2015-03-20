using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Octostache;

namespace Calamari.Integration.ConfigurationVariables
{
    public class ConfigurationVariablesReplacer
    {
        public void ModifyConfigurationFile(string configurationFilePath, VariableDictionary variables)
        {
            XDocument doc;

            using (var reader = XmlReader.Create(configurationFilePath))
            {
                doc = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
            }

            var changes = new List<string>();

            foreach (var variable in variables.GetNames())
            {
                changes.AddRange(
                    ReplaceAppSettingOrConnectionString(doc, "//*[local-name()='appSettings']/*[local-name()='add']", "key", variable, "value", variables.Get(variable)).Concat(
                    ReplaceAppSettingOrConnectionString(doc, "//*[local-name()='connectionStrings']/*[local-name()='add']", "name", variable, "connectionString", variables.Get(variable)).Concat(
                    ReplaceStonglyTypeApplicationSetting(doc, "//*[local-name()='applicationSettings']//*[local-name()='setting']", "name", variable, variables.Get(variable)))));
            }

            if (!changes.Any())
            {
                Console.WriteLine("No matching setting or connection string names were found in: {0}", configurationFilePath);
                return;
            }

            Console.WriteLine("Updating appSettings and connectionStrings in: {0}", configurationFilePath);

            foreach (var change in changes)
            {
                CalamariLogger.Verbose(change);
            }

            var xws = new XmlWriterSettings { OmitXmlDeclaration = doc.Declaration == null, Indent = true };
            using (var writer = XmlWriter.Create(configurationFilePath, xws))
            {
                doc.Save(writer);
            }
        }

        static IEnumerable<string> ReplaceAppSettingOrConnectionString(XNode document, string xpath, string keyAttributeName, string keyAttributeValue, string valueAttributeName, string value)
        {
            var changes = new List<string>();
            var settings =
                from element in document.XPathSelectElements(xpath)
                let keyAttribute = element.Attribute(keyAttributeName)
                where keyAttribute != null
                where string.Equals(keyAttribute.Value, keyAttributeValue, StringComparison.InvariantCultureIgnoreCase)
                select element;

            value = value ?? string.Empty;

            foreach (var setting in settings)
            {
                changes.Add(string.Format("Setting '{0}' = '{1}'", keyAttributeValue, value));

                var valueAttribute = setting.Attribute(valueAttributeName);
                if (valueAttribute == null)
                {
                    setting.Add(new XAttribute(valueAttributeName, value));
                }
                else
                {
                    valueAttribute.SetValue(value);
                }
            }

            return changes;
        }

        static IEnumerable<string> ReplaceStonglyTypeApplicationSetting(XNode document, string xpath, string keyAttributeName, string keyAttributeValue, string value)
        {
            var changes = new List<string>();

            var settings =
                from element in document.XPathSelectElements(xpath)
                let keyAttribute = element.Attribute(keyAttributeName)
                where keyAttribute != null
                where string.Equals(keyAttribute.Value, keyAttributeValue, StringComparison.InvariantCultureIgnoreCase)
                select element;

            value = value ?? string.Empty;

            foreach (var setting in settings)
            {
                changes.Add(string.Format("Setting '{0}' = '{1}'", keyAttributeValue, value));

                var valueElement = setting.Elements().FirstOrDefault(e => e.Name.LocalName == "value");
                if (valueElement == null)
                {
                    setting.Add(new XElement("value", value));
                }
                else
                {
                    valueElement.SetValue(value);
                }
            }

            return changes;
        }
    }
}
