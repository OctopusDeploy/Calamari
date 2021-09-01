using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Sashimi.Server.Contracts.ActionHandlers
{
    public class OutputVariableCollection : ICollection<OutputVariable>, IReadOnlyDictionary<string, OutputVariable>
    {
        readonly Dictionary<string, OutputVariable> items = new(StringComparer.OrdinalIgnoreCase);

        public int Count => items.Count;

        public OutputVariable this[string name]
        {
            get => items[name];
            set => items[name] = value;
        }

        public IEnumerable<string> Keys => items.Keys;
        public IEnumerable<OutputVariable> Values => items.Values;

        bool ICollection<OutputVariable>.IsReadOnly => false;

        public void Add(OutputVariable item)
        {
            items.Add(item.Name, item);
        }

        public bool ContainsKey(string name)
        {
            return items.ContainsKey(name);
        }

        public bool TryGetValue(string name,
                                [MaybeNullWhen(false)]
                                out OutputVariable value)
        {
            return items.TryGetValue(name, out value);
        }

        public void Clear()
        {
            items.Clear();
        }

        bool ICollection<OutputVariable>.Contains(OutputVariable item)
        {
            return items.ContainsKey(item.Name);
        }

        public void CopyTo(OutputVariable[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        bool ICollection<OutputVariable>.Remove(OutputVariable item)
        {
            return items.Remove(item.Name);
        }

        IEnumerator<OutputVariable> IEnumerable<OutputVariable>.GetEnumerator()
        {
            return GetEnumerator();
        }

        IEnumerator<KeyValuePair<string, OutputVariable>> IEnumerable<KeyValuePair<string, OutputVariable>>.GetEnumerator()
        {
            return items.GetEnumerator();
        }

        IEnumerator<OutputVariable> GetEnumerator()
        {
            return items.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}