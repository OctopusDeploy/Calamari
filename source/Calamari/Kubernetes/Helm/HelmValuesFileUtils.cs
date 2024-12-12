using System.Collections.Generic;
using System.Linq;

namespace Calamari.Kubernetes.Helm
{
    public static class HelmValuesFileUtils
    {
        public static string GetUniqueFileName(string prefix, int? index)
        {
            return index != null
                ? $"{prefix}-{index}.yaml"
                : $"{prefix}.yaml";
        }

        public static IList<string> SplitValuesFilePaths(string valuesFilePaths)
        {
            return valuesFilePaths?
                              .Split('\r', '\n')
                              .Where(x => !string.IsNullOrWhiteSpace(x))
                              .Select(x => x.Trim())
                              .ToList();
        }
    }
}