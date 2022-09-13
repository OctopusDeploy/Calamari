using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Features.ConfigurationVariables
{
    public class ConfigurationVariablesReplacer : IConfigurationVariablesReplacer
    {
        readonly ILog log;
        readonly bool ignoreVariableReplacementErrors;

        public ConfigurationVariablesReplacer(IVariables variables, ILog log)
        {
            this.log = log;

            ignoreVariableReplacementErrors = variables.GetFlag(KnownVariables.Package.IgnoreVariableReplacementErrors);
        }

        public void ModifyConfigurationFile(string configurationFilePath, IVariables variables)
        {
            try
            {
                var doc = ReadXmlDocument(configurationFilePath);
                var changes = ApplyChanges(doc, variables);

                if (!changes.Any())
                {
                    log.InfoFormat("No matching appSetting, applicationSetting, nor connectionString names were found in: {0}", configurationFilePath);
                    return;
                }

                log.InfoFormat("Updating appSettings, applicationSettings, and connectionStrings in: {0}", configurationFilePath);

                foreach (var change in changes)
                {
                    log.Verbose(change);
                }

                WriteXmlDocument(doc, configurationFilePath);
            }
            catch (Exception ex)
            {
                if (ignoreVariableReplacementErrors)
                {
                    log.Warn(ex.Message);
                    log.Warn(ex.StackTrace);
                }
                else
                {
                    log.ErrorFormat("Exception while replacing configuration-variables in: {0}", configurationFilePath);
                    throw;
                }
            }
        }

        static XDocument ReadXmlDocument(string configurationFilePath)
        {
            using (var reader = XmlReader.Create(configurationFilePath, XmlUtils.DtdSafeReaderSettings))
            {
                return XDocument.Load(reader, LoadOptions.PreserveWhitespace);
            }
        }

        static List<string> ApplyChanges(XNode doc, IVariables variables)
        {
            var changes = new List<string>();

            foreach (var variable in variables.GetNames())
            {
                changes.AddRange(
                    ReplaceAppSettingOrConnectionString(doc, "//*[local-name()='appSettings']/*[local-name()='add']", "key", variable, "value", variables).Concat(
                        ReplaceAppSettingOrConnectionString(doc, "//*[local-name()='connectionStrings']/*[local-name()='add']", "name", variable, "connectionString", variables).Concat(
                            ReplaceStronglyTypeApplicationSetting(doc, "//*[local-name()='applicationSettings']//*[local-name()='setting']", "name", variable, variables))));
            }
            return changes;
        }

        static void WriteXmlDocument(XDocument doc, string configurationFilePath)
        {
            var xws = new XmlWriterSettings {OmitXmlDeclaration = doc.Declaration == null, Indent = true};
            using (var file = new FileStream(configurationFilePath, FileMode.Create, FileAccess.Write))
            using (var writer = XmlWriter.Create(file, xws))
            {
                doc.Save(writer);
            }
        }

        static IEnumerable<string> ReplaceAppSettingOrConnectionString(XNode document, string xpath, string keyAttributeName, string keyAttributeValue, string valueAttributeName, IVariables variables)
        {
            var changes = new List<string>();
            var settings = (
                from element in document.XPathSelectElements(xpath)
                let keyAttribute = element.Attribute(keyAttributeName)
                where keyAttribute != null
                where string.Equals(keyAttribute.Value, keyAttributeValue, StringComparison.OrdinalIgnoreCase)
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

        static IEnumerable<string> ReplaceStronglyTypeApplicationSetting(XNode document, string xpath, string keyAttributeName, string keyAttributeValue, IVariables variables)
        {
            var changes = new List<string>();

            var settings = (
                from element in document.XPathSelectElements(xpath)
                let keyAttribute = element.Attribute(keyAttributeName)
                where keyAttribute != null
                where string.Equals(keyAttribute.Value, keyAttributeValue, StringComparison.OrdinalIgnoreCase)
                select element).ToList();

            if (settings.Count == 0)
                return changes;

            var value = variables.Get(keyAttributeValue) ?? string.Empty;

            foreach (var setting in settings)
            {
                changes.Add($"Setting '{keyAttributeValue}' = '{value}'");

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
