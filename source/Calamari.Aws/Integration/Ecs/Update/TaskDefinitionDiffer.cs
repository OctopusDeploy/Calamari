using System;
using System.Text;
using Amazon.ECS.Model;
using Newtonsoft.Json;

namespace Calamari.Aws.Integration.Ecs.Update;

public static class TaskDefinitionDiffer
{
    public const string NoChangesMessage = "No changes were detected in the task definition.";

    static readonly JsonSerializerSettings Settings = new()
    {
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Ignore
    };

    public static string Diff(TaskDefinition before, TaskDefinition after)
    {
        var b = JsonConvert.SerializeObject(before, Settings);
        var a = JsonConvert.SerializeObject(after, Settings);

        if (b == a)
        {
            return NoChangesMessage;
        }

        var beforeLines = b.Split('\n');
        var afterLines = a.Split('\n');
        var maxLines = Math.Max(beforeLines.Length, afterLines.Length);

        var sb = new StringBuilder();
        for (var i = 0; i < maxLines; i++)
        {
            var bl = i < beforeLines.Length ? beforeLines[i] : null;
            var al = i < afterLines.Length ? afterLines[i] : null;
            if (bl == al)
            {
                sb.Append("  ").Append(bl).Append('\n');
            }
            else
            {
                if (bl is not null) sb.Append("- ").Append(bl).Append('\n');
                if (al is not null) sb.Append("+ ").Append(al).Append('\n');
            }
        }
        return sb.ToString().TrimEnd('\n');
    }
}
