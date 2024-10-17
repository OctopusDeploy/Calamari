using System;

namespace Calamari.Azure.AppServices
{
    public class AppSetting
    {
        public string Name { get; set; }
        public string Value { get; set; }

        public bool SlotSetting { get; set; }

        public void Deconstruct(out string name, out string value, out bool isSlotSetting)
        {
            name = Name;
            value = Value;
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
                SlotSetting
            }.GetHashCode();
        }

        public override string ToString()
        {
            return $"\nName: {Name}\nValue: {Value}\nIsSlotSetting: {SlotSetting}\n";
        }
    }
}