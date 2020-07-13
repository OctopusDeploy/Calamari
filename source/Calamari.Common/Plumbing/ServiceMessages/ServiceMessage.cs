using System;
using System.Collections.Generic;

namespace Calamari.Common.Plumbing.ServiceMessages
{
    public class ServiceMessage
    {
        readonly string name;
        readonly Dictionary<string, string> properties;

        public ServiceMessage(string name, Dictionary<string, string> properties = null)
        {
            this.name = name;
            this.properties = properties ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public string Name
        {
            get { return name; }
        }

        public IDictionary<string, string> Properties
        {
            get { return properties; }
        }

        public string GetValue(string key)
        {
            string s;
            return properties.TryGetValue(key, out s) ? s : null;
        }
    }
}