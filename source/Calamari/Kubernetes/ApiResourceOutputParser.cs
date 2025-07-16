using System;
using System.Collections.Generic;
using System.Linq;

namespace Calamari.Kubernetes
{
    public static class ApiResourceOutputParser
    {
        const char SpaceChar = ' ';
        const string KindHeader = "KIND";
        const string NamespacedHeader = "NAMESPACED";
        const string ApiVersionHeader = "APIVERSION";

        public static Dictionary<ApiResourceIdentifier, bool> ParseKubectlApiResourceOutput(List<string> outputLines)
        {
            var headerRow = outputLines.First();

            //validate that the header row has what we want
            if (!headerRow.Contains(KindHeader) || !headerRow.Contains(NamespacedHeader) || !headerRow.Contains(ApiVersionHeader))
            {
                throw new InvalidOperationException("kubectl api-resources did not have a valid header row");
            }

            //determine the size of each column based on the header row
            var columns = new List<Column>();

            var activeColumn = new Column();

            var previousChar = SpaceChar;
            for (var i = 0; i < headerRow.Length; i++)
            {
                var c = headerRow[i];

                //if this is a non-space char and the previous char was not a space, continue building up the name
                if (c != ' ' && previousChar != SpaceChar)
                {
                    activeColumn.Name += c;
                }
                else if (c != SpaceChar)
                {
                    //if this the first non-space char _after_ a char
                    //create a new active column
                    activeColumn = new Column
                    {
                        Name = c.ToString(),
                        StartIndex = i
                    };
                    columns.Add(activeColumn);
                }

                activeColumn.EndIndex = i;
                previousChar = c;
            }

            var kindColumn = columns.Single(c => c.Name == KindHeader);
            var apiVersionColumn = columns.Single(c => c.Name == ApiVersionHeader);
            var namespacedColumn = columns.Single(c => c.Name == NamespacedHeader);

            return outputLines
                   .Skip(1) //skip the header row 
                   .Select(row =>
                           {
                               var kind = row.Substring(kindColumn.StartIndex, kindColumn.Length).TrimEnd();
                               var apiVersion = row.Substring(apiVersionColumn.StartIndex, apiVersionColumn.Length).TrimEnd();
                               var namespaced = row.Substring(namespacedColumn.StartIndex, namespacedColumn.Length).TrimEnd();

                               return (new ApiResourceIdentifier(apiVersion, kind), bool.TryParse(namespaced, out var isNamespaced) && isNamespaced);
                           })
                   .ToDictionary(x => x.Item1, x => x.Item2);
        }

        class Column
        {
            public string Name { get; set; }
            public int StartIndex { get; set; }
            public int EndIndex { get; set; }

            public int Length => EndIndex - StartIndex;
        }
    }
}