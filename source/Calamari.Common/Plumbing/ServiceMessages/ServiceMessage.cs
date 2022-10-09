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