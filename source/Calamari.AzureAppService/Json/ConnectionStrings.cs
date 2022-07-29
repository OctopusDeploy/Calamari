using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Management.WebSites.Models;
using Newtonsoft.Json;

namespace Calamari.AzureAppService.Json
{
    public class ConnectionStringSetting
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public ConnectionStringType Type { get; set; }

        public bool SlotSetting { get; set; }

        internal void Deconstruct(out string name, out string value, out ConnectionStringType type, out bool isSlotSetting)
        {
            name = Name;
            value = Value;
            type = Type;
            isSlotSetting = SlotSetting;
        }

        public bool Equals(AppSetting other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Name == other.Name && Value == other.Value && SlotSetting == other.SlotSetting;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == this.GetType() && Equals((AppSetting) obj);
        }

        public override int GetHashCode()
        {
            return new
            {
                Name,
                Value,
                SlotSetting,
                Type
            }.GetHashCode();
        }

        public override string ToString()
        {
            return $"\nName: {Name}\nValue: {Value}\nType: {Type}\nIsSlotSetting: {SlotSetting}\n";
        }
    }
}