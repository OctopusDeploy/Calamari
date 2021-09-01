using System;
using System.Collections.Generic;
using System.Linq;

namespace Sashimi.Server.Contracts.ActionHandlers
{
    public class ScriptOutputAction
    {
        public ScriptOutputAction(string name, IDictionary<string, string> properties)
        {
            Name = name;
            Properties = properties;
        }

        public string Name { get; }

        public IDictionary<string, string> Properties { get; }

        public bool ContainsPropertyWithValue(string propertyName)
        {
            return Properties.ContainsKey(propertyName) && !string.IsNullOrEmpty(Properties[propertyName]);
        }

        public bool ContainsPropertyWithGuid(string propertyName)
        {
            return ContainsPropertyWithValue(propertyName) && IsGuid(propertyName);
        }

        bool IsGuid(string propertyName)
        {
            return Guid.TryParse(Properties[propertyName], out _);
        }

        public string[] GetStrings(params string[] propertyNames)
        {
            var values = Properties.Where(x => propertyNames.Contains(x.Key))
                                   .Select(x => x.Value)
                                   .ToList();
            if (!values.Any())
                return Array.Empty<string>();

            var allValues = new List<string>();
            foreach (var v in values.Where(v => !string.IsNullOrWhiteSpace(v)))
                allValues.AddRange(v.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(_ => _.Trim()));
            return allValues.ToArray();
        }
    }
}