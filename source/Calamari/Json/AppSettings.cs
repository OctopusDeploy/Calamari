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

    public class AppSetting
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
    }
}