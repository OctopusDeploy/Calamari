using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Sashimi.Aws.CloudTemplates
{
    static class AwsDataTypes
    {
        public const string CloudFormationTemplateTypeName = "CloudFormationTemplateParameters";
        const string DefaultType = "string";

        static readonly IReadOnlyList<(Regex regex, string type)> TypeMap = new[]
        {
            (new Regex("String"), "string"),
            (new Regex("CommaDelimitedList"), "string[]"),
            (new Regex("List<Number>"), "int[]"),
            (new Regex("List<.*?>"), "string[]"),
            (new Regex("Number"), "int")
        };

        public static string MapToType(string awsType)
        {
            if (awsType == null)
                return DefaultType;

            return TypeMap
                    .Where(entry => entry.regex.IsMatch(awsType))
                    .Select(entry => entry.type)
                    .FirstOrDefault()
                ?? DefaultType;
        }
    }
}