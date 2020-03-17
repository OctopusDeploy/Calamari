using System;
using System.Collections.Generic;

namespace Sashimi.Terraform.CloudTemplates
{
    public static class TerraformDataTypes
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
            { "map", RawMap }
        };

        public static string MapToType(string awsType)
        {
            if (awsType == null) return DefaultType;
            return TypeMap.ContainsKey(awsType) ? TypeMap[awsType] : DefaultType;
        }
    }
}