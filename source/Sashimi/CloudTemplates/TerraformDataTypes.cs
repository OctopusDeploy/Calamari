using System;
using System.Collections.Generic;
using System.Linq;

namespace Sashimi.Terraform.CloudTemplates
{
    static class TerraformDataTypes
    {
        public const string TerraformTemplateTypeName = "TerraformTemplateParameters";
        public static string DefaultType = "string";
        public static string RawPrefix = "raw_";
        public static string RawList = RawPrefix + "list";
        public static string RawMap = RawPrefix + "map";

        public static IDictionary<string, string> TypeMap = new Dictionary<string, string>
        {
            { "string", "string" },
            { "list", RawList },
            { "tuple", RawList },
            { "set", RawList },
            { "map", RawMap },
            { "object", RawMap }
        };

        public static string MapToType(string terraformType)
        {
            if (terraformType == null) return DefaultType;

            return TypeMap.Where(kv => terraformType.StartsWith(kv.Key))
                   .Select(kv => kv.Value)
                   .FirstOrDefault()
                ?? DefaultType;
        }
    }
}