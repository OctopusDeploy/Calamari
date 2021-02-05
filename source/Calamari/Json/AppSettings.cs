using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Calamari.AzureAppService.Json
{
    public class AppSettingsRoot
    {
        public IEnumerable<AppSetting> AppSettings { get; set; }

        [JsonIgnore]
        public bool HasSettings => AppSettings.Any();
    }

    public class AppSetting : IEquatable<AppSetting>
    {
        public string Name { get; set; }

        public string Value { get; set; }

        public bool IsSlotSetting { get; set; }

        internal void Deconstruct(out string name, out string value, out bool isSlotSetting)
        {
            name = Name;
            value = Value;
            isSlotSetting = IsSlotSetting;
        }

        public bool Equals(AppSetting other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Name == other.Name && Value == other.Value && IsSlotSetting == other.IsSlotSetting;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == this.GetType() && Equals((AppSetting) obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Name, Value, IsSlotSetting);
        }
    }
}