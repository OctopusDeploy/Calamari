using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Calamari.Common.Plumbing.Commands.Options;

public sealed partial class OptionSet
{
    const int DescriptionColumn = 29;

    [GeneratedRegex(@"^(?<flag>--|-|/)(?<name>[^:=]+)(?:[:=](?<value>.*))?$")]
    private static partial Regex FlagPattern();

    readonly Dictionary<string, Entry> entries = new(StringComparer.OrdinalIgnoreCase);

    public OptionSet Add(string prototype, string? description, Action<string> action)
    {
        ArgumentNullException.ThrowIfNull(prototype);
        ArgumentNullException.ThrowIfNull(action);
        if (prototype.Length == 0) throw new ArgumentException("Cannot be the empty string.", nameof(prototype));

        var needsValue = prototype.EndsWith('=');
        var name = needsValue ? prototype[..^1] : prototype;
        if (name.Length == 0) throw new ArgumentException("Option name cannot be empty.", nameof(prototype));

        entries[name] = new Entry(needsValue, description, action);
        return this;
    }

    public void WriteOptionDescriptions(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        foreach (var (name, entry) in entries)
        {
            var prototype = entry.NeedsValue ? $"  --{name}=VALUE" : $"  --{name}";
            if (string.IsNullOrWhiteSpace(entry.Description))
            {
                writer.WriteLine(prototype);
                continue;
            }

            if (prototype.Length >= DescriptionColumn)
                writer.WriteLine($"{prototype}  {entry.Description}");
            else
                writer.WriteLine($"{prototype}{new string(' ', DescriptionColumn - prototype.Length)}{entry.Description}");
        }
    }

    public List<string> Parse(IEnumerable<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var remaining = new List<string>();
        Entry? pending = null;
        string? pendingName = null;

        foreach (var argument in arguments)
        {
            if (pending != null)
            {
                pending.Action(argument);
                pending = null;
                pendingName = null;
                continue;
            }

            var match = FlagPattern().Match(argument);
            if (!match.Success)
            {
                remaining.Add(argument);
                continue;
            }

            var name = match.Groups["name"].Value;
            if (!entries.TryGetValue(name, out var entry))
            {
                remaining.Add(argument);
                continue;
            }

            if (!entry.NeedsValue)
            {
                entry.Action(name);
                continue;
            }

            if (match.Groups["value"].Success)
            {
                entry.Action(match.Groups["value"].Value);
            }
            else
            {
                pending = entry;
                pendingName = name;
            }
        }

        return pending != null ? throw new OptionException($"Missing required value for option '{pendingName}'.") : remaining;
    }

    sealed record Entry(bool NeedsValue, string? Description, Action<string> Action);
}
