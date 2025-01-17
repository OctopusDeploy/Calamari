using System;
using System.Collections.Generic;

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

    public class properties
    {
        public IEnumerable<string> appSettingNames { get; set; }
        public IEnumerable<string> connectionStringNames { get; set; }
    }
}
