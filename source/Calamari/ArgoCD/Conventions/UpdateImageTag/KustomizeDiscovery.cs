using System;
using System.IO;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.ArgoCD.Conventions.UpdateImageTag;

public class KustomizeDiscovery(ILog log)
{
    
    public static string? TryFindKustomizationFile(ICalamariFileSystem fileSystem, string rootPath)
    {
        foreach (var fileName in ArgoCDConstants.KustomizationFileNames)
        {
            var absPath = Path.Combine(rootPath, fileName);
            if (fileSystem.FileExists(absPath))
            {
                return absPath;
            }
        }

        return null;
    }
    
    public PatchType? DeterminePatchType(string content)
    {
        if (KustomizationValidator.IsKustomizationResource(content))
            return null;

        if (IsJson6902PatchContent(content))
            return PatchType.Json6902;

        if (IsStrategicMergePatchContent(content))
            return PatchType.StrategicMerge;

        return null;
    }
    
    public bool IsJson6902PatchContent(string content)
    {
        try
        {

            var trimmedContent = content.Trim();

            if (trimmedContent.StartsWith("[") || trimmedContent.StartsWith("-"))
            {

                var hasOpField = System.Text.RegularExpressions.Regex.IsMatch(content,
                                                                              @"[""']?op[""']?\s*:\s*[""']?(add|remove|replace|move|copy|test)[""']?",
                                                                              System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                var hasPathField = content.Contains("path") || content.Contains("\"path\"") || content.Contains("'path'");

                return hasOpField && hasPathField;
            }

            return false;
        }
        catch (Exception ex)
        {
            log.Verbose($"Error determining if content is JSON 6902 patch: {ex.Message}");
            return false;
        }
    }

    public bool IsStrategicMergePatchContent(string content)
    {
        try
        {

            var hasKubernetesFields = System.Text.RegularExpressions.Regex.IsMatch(content,
                                                                                   @"[""']?(apiVersion|kind|metadata|spec|data)[""']?\s*:",
                                                                                   System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            var hasImageReferences = System.Text.RegularExpressions.Regex.IsMatch(content,
                                                                                  @"[""']?(image|containers)[""']?\s*:",
                                                                                  System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            return hasKubernetesFields || hasImageReferences;
        }
        catch (Exception ex)
        {
            log.Verbose($"Error determining if content is strategic merge patch: {ex.Message}");
            return false;
        }
    }
}