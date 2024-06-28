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
    }
}