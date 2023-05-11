using Calamari.Common.Plumbing.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Calamari.Common.Plumbing.ServiceMessages
{
    public class ServiceMessage
    {
        public const string ServiceMessageLabel = "##octopus";

        readonly Dictionary<string, string> properties;

        public static ServiceMessage ParseRawServiceMessage(string serviceMessageLog)
        {
            serviceMessageLog = serviceMessageLog.Split('[')[1].Split(']')[0];
            var parts = serviceMessageLog.Split(" ");
            var serviceMessageType = parts[0];
            var properties = parts.Skip(1).Select(s =>
            {
                var key = s.Substring(0, s.IndexOf('='));
                var value = s.Substring(s.IndexOf('=') + 1).Trim('\'', '\"');
                return (Key: key, Value: value);
            });
            return new ServiceMessage(serviceMessageType,
                properties.ToDictionary(x => x.Key, x =>
                {
                    return AbstractLog.UnconvertServiceMessageValue(x.Value);
                }));
        }

        public ServiceMessage(string name, Dictionary<string, string>? properties = null)
        {
            this.Name = name;
            this.properties = properties ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        private ServiceMessage(string name, params (string, string)[] parameters)
        {
            this.Name = name;
            this.properties = parameters.ToDictionary(kvp => kvp.Item1, kvp => kvp.Item2);
        }

        public string Name { get; }

        public IDictionary<string, string> Properties => properties;

        public static ServiceMessage Create(string name, params (string, string)[] parameters) =>
            new ServiceMessage(name, parameters);

        public string? GetValue(string key)
        {
            string s;
            return properties.TryGetValue(key, out s) ? s : null;
        }

        public override string ToString()
        {
            var parameters = properties
                .Where(kvp => kvp.Value != null)
                .Select(kvp => $"{kvp.Key}=\"{AbstractLog.ConvertServiceMessageValue(kvp.Value)}\"");
            return $"{ServiceMessageLabel}[{Name} {string.Join(" ", parameters)}]";
        }
    }
}