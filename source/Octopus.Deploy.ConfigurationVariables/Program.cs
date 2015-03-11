using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Octopus.Deploy.Startup;
using Octopus.Deploy.Variables;

namespace Octopus.Deploy.ConfigurationVariables
{
    class Program
    {
        static int Main(string[] args)
        {
            try
            {
                string variablesFilePath = null;

                var options = new OptionSet();
                options.Add("variablesFile=", "Path to a variables file", v => variablesFilePath = Path.GetFullPath(v));

                var files = options.Parse(args);
                if (files.Count == 0)
                {
                    Console.WriteLine("No input files specified");
                    return 0;
                }

                if (string.IsNullOrWhiteSpace(variablesFilePath))
                {
                    throw new ArgumentException("No variables file was specified");
                }

                if (!File.Exists(variablesFilePath))
                {
                    throw new ArgumentException(string.Format("Couldn't find variables file at '{0}'", variablesFilePath));
                }

                var variables = VariablesFileFormat.ReadFrom(variablesFilePath);

                foreach (var file in files)
                {
                    ReplaceConfigurationValues(file, variables);
                }

                return 0;
            }
            catch (Exception ex)
            {
                return ConsoleFormatter.PrintError(ex);
            }
        }

        static void ReplaceConfigurationValues(string configurationFilePath, Dictionary<string, string> variables)
        {
            XDocument doc;

            using (var reader = XmlReader.Create(configurationFilePath))
            {
                doc = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
            }

            var changes = new List<string>();

            foreach (var variable in variables)
            {
                changes.AddRange(
                    ReplaceAttributeValues(doc, "//*[local-name()='appSettings']/*[local-name()='add']", "key", variable.Key, "value", variable.Value).Concat(
                    ReplaceAttributeValues(doc, "//*[local-name()='connectionStrings']/*[local-name()='add']", "name", variable.Key, "connectionString", variable.Value).Concat(
                    ReplaceAppSettingsValues(doc, "//*[local-name()='applicationSettings']//*[local-name()='setting']", "name", variable.Key, variable.Value))));
            }

            if (!changes.Any())
            {
                Console.WriteLine("No matching setting or connection string names were found in: {0}", configurationFilePath);
                return;
            }

            Console.WriteLine("Updating appSettings and connectionStrings in: {0}", configurationFilePath);

            foreach (var change in changes)
            {
                Console.WriteLine("##octopus[stdout-verbose]");
                Console.WriteLine(change);
                Console.WriteLine("##octopus[stdout-default]");
            }

            var xws = new XmlWriterSettings { OmitXmlDeclaration = doc.Declaration == null, Indent = true };
            using (var writer = XmlWriter.Create(configurationFilePath, xws))
            {
                doc.Save(writer);
            }
        }

        static IEnumerable<string> ReplaceAttributeValues(XNode document, string xpath, string keyAttributeName, string keyAttributeValue, string valueAttributeName, string value)
        {
            var result = new List<string>();
            var settings =
                from element in document.XPathSelectElements(xpath)
                let keyAttribute = element.Attribute(keyAttributeName)
                where keyAttribute != null
                where string.Equals(keyAttribute.Value, keyAttributeValue, StringComparison.InvariantCultureIgnoreCase)
                select element;

            value = value ?? string.Empty;

            foreach (var setting in settings)
            {
                result.Add(string.Format("Setting '{0}' = '{1}'", keyAttributeValue, value));

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

            return result;
        }

        static IEnumerable<string> ReplaceAppSettingsValues(XNode document, string xpath, string keyAttributeName, string keyAttributeValue, string value)
        {
            var result = new List<string>();

            var settings =
                from element in document.XPathSelectElements(xpath)
                let keyAttribute = element.Attribute(keyAttributeName)
                where keyAttribute != null
                where string.Equals(keyAttribute.Value, keyAttributeValue, StringComparison.InvariantCultureIgnoreCase)
                select element;

            value = value ?? string.Empty;

            foreach (var setting in settings)
            {
                result.Add(string.Format("Setting '{0}' = '{1}'", keyAttributeValue, value));

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

            return result;
        }
    }
}
