using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Octopus.Deploy.Variables
{
    public static class VariablesFileFormat
    {
        public static Dictionary<string, string> ReadFrom(string variablesFilePath)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using (var targetStream = new FileStream(variablesFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new StreamReader(targetStream))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (String.IsNullOrEmpty(line))
                    {
                        continue;
                    }


                    var parts = line.Split(',');
                    var name = Encoding.UTF8.GetString(Convert.FromBase64String(parts[0]));
                    var value = Encoding.UTF8.GetString(Convert.FromBase64String(parts[1]));
                    result[name] = value;
                }
            }

            return result;
        } 
    }
}
