using System;
using System.Collections.Generic;
using System.Text;

namespace Sashimi.Server.Contracts.ActionHandlers
{
    public class ServiceMessage
    {
        readonly Dictionary<string, string> properties;

        public ServiceMessage(string name, Dictionary<string, string>? properties = null)
        {
            Name = name;
            this.properties = properties ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public string Name { get; }

        public IDictionary<string, string> Properties => properties;

        public static string EncodeValue(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        }

        public string? GetValue(string key)
        {
            return properties.TryGetValue(key, out var s) ? s : null;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("##octopus[").Append(Name);

            foreach (var (key, value) in properties)
                sb.Append(" ").Append(key).Append("=\"").Append(EncodeValue(value)).Append("\"");

            sb.Append("]");

            return sb.ToString();
        }
    }
}