using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

/*
 * JSON format
 * "properties":{
 *  "appSettingNames":[
 *  "string1",
 *  "string2"
 *  ]
 *}
 *
 */

namespace Calamari.AzureAppService.Json
{
    public class appSettingNamesRoot
    {
        public string name { get; set; }
        
        public string type => "Microsoft.Web/sites";

        public properties properties { get; set; }
        
    }

#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
    public class properties
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
    {
        public IEnumerable<string> appSettingNames { get; set; }
        public IEnumerable<string> connectionStringNames { get; set; }
    }
}
