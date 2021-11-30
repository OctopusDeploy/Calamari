using System;
using Newtonsoft.Json;

namespace Calamari.Common.Plumbing.Deployment.PackageRetention
{
    public class CacheAge
    {
        public int Value { get; private set; }

        [JsonConstructor]
        public CacheAge(int value)
        {
            Value = value;
        }

        public void IncrementAge()
        {
            Value++;
        }
    }
}