using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Calamari.AzureAppService.Json
{
    public class AppSetting
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public bool SlotSetting { get; set; }
    }
}