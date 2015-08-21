using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Calamari.Util;
using Octostache;

namespace Calamari.Integration.ConfigurationVariables
{
    public class ConfigurationVariablesReplacer : IConfigurationVariablesReplacer
    {
        public void ModifyConfigurationFile(string configurationFilePath, VariableDictionary variables)
        {
            Log.VerboseFormat("Looking for configuration variables in: {0}", configurationFilePath);

            XDocument doc;

            using (var reader = XmlReader.Create(configurationFilePath, XmlUtils.DtdSafeReaderSettings))
            {
                doc = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
            }

            var changes = new List<string>();

            foreach (var variable in variables.GetNames())
            {
                changes.AddRange(
                    ReplaceAppSettingOrConnectionString(doc, "//*[local-name()='appSettings']/*[local-name()='add']", "key", variable, "value", variables).Concat(
                    ReplaceAppSettingOrConnectionString(doc, "//*[local-name()='connectionStrings']/*[local-name()='add']", "name", variable, "connectionString", variables).Concat(
                    ReplaceStonglyTypeApplicationSetting(doc, "//*[local-name()='applicationSettings']//*[local-name()='setting']", "name", variable, variables))));
            }

            if (!changes.Any())
            {
                Log.Info("No matching setting or connection string names were found in: {0}", configurationFilePath);
                return;
            }

            Log.Info("Updating appSettings and connectionStrings in: {0}", configurationFilePath);

            foreach (var change in changes)
            {
                Log.Verbose(change);
            }

            var xws = new XmlWriterSettings { OmitXmlDeclaration = doc.Declaration == null, Indent = true };
            using (var writer = XmlWriter.Create(configurationFilePath, xws))
            {
                doc.Save(writer);
            }
        }

        static IEnumerable<string> ReplaceAppSettingOrConnectionString(XNode document, string xpath, string keyAttributeName, string keyAttributeValue, string valueAttributeName, VariableDictionary variables)
        {
            var changes = new List<string>();
            var settings = (
                from element in document.XPathSelectElements(xpath)
                let keyAttribute = element.Attribute(keyAttributeName)
                where keyAttribute != null
                where string.Equals(keyAttribute.Value, keyAttributeValue, StringComparison.InvariantCultureIgnoreCase)
                select element).ToList();

            if (settings.Count == 0)
                return changes;

            var value = variables.Get(keyAttributeValue) ?? string.Empty;

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

        static IEnumerable<string> ReplaceStonglyTypeApplicationSetting(XNode document, string xpath, string keyAttributeName, string keyAttributeValue, VariableDictionary variables)
        {
            var changes = new List<string>();

            var settings = (
                from element in document.XPathSelectElements(xpath)
                let keyAttribute = element.Attribute(keyAttributeName)
                where keyAttribute != null
                where string.Equals(keyAttribute.Value, keyAttributeValue, StringComparison.InvariantCultureIgnoreCase)
                select element).ToList();

            if (settings.Count == 0)
                return changes;

            var value = variables.Get(keyAttributeValue) ?? string.Empty;

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
