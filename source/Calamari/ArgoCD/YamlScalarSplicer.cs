#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Calamari.Common.Plumbing.Extensions;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Calamari.ArgoCD
{
    public record YamlScalarEdit(YamlScalarNode Node, string NewValue);

    /// <summary>
    /// Replaces scalar values by splicing into the original YAML text. Re-emitting a parsed document
    /// drops comments and blank lines and reflows indentation, quoting and folded scalars, which turns
    /// a one-line image tag change into an unreviewable whole-file diff. Splicing leaves every byte
    /// outside the replaced value exactly as it was, line endings included.
    /// </summary>
    public static class YamlScalarSplicer
    {
        /// <summary>
        /// Why a value cannot be replaced, phrased for the person whose file it is. Callers add their
        /// own context (which image, which file) and raise it as a CommandException.
        /// </summary>
        public static string DescribeUnsupportedValue(YamlScalarNode node)
        {
            return node.Style == ScalarStyle.Folded
                ? "it is written as a folded block scalar (>), which does not record where the original line breaks were. Updating it would collapse the block onto one line and reformat the file. Use a literal block (|) or a plain value instead"
                : $"the layout of this {node.Style} scalar could not be interpreted, so updating it could corrupt the file";
        }

        public static string ReplaceValue(string document, YamlScalarNode node, string newValue)
        {
            return ReplaceValues(document, new[] { new YamlScalarEdit(node, newValue) });
        }

        /// <summary>
        /// True when this scalar's value can be replaced by splicing. Callers MUST check before
        /// reporting an image as updated: an unsupported style, or a position we cannot verify,
        /// leaves the file untouched rather than corrupting it or failing the step.
        /// </summary>
        public static bool CanReplaceValue(string document, YamlScalarNode node)
        {
            return node.Style == ScalarStyle.Literal
                ? TryGetBlockRegion(document, node) != null
                : TryGetInlineValueRegion(document, node, out _, out _);
        }

        /// <summary>
        /// Locates a block scalar's indented content and works out how much of the leading whitespace
        /// is structure. Rendering the parser's own value back must then reproduce the original
        /// content, so a block we cannot describe is refused rather than corrupted. Line endings are
        /// excluded from that comparison: a block whose own endings differ from the rest of the file is
        /// harmonised to the document's ending, which still catches indentation differences.
        /// </summary>
        static BlockRegion? TryGetBlockRegion(string document, YamlScalarNode node)
        {
            if (node.End.Line <= node.Start.Line)
                return null;

            var start = OffsetOfLine(document, (int)node.Start.Line + 1);
            var end = OffsetOfLine(document, (int)node.End.Line) + (int)node.End.Column - 1;

            if (end < start || end > document.Length)
                return null;

            var region = document[start..end];
            var value = node.Value ?? "";

            var indent = StructuralIndent(region, value);
            if (indent == null)
                return null;

            var block = new BlockRegion(start, end, indent, region.HasTrailingNewLine());

            return Render(value, block, document).ReplaceLineEndings("\n") == region.ReplaceLineEndings("\n")
                ? block
                : null;
        }

        record BlockRegion(int Start, int End, string Indent, bool EndsWithBreak);

        /// <summary>
        /// Locates the value inside an inline scalar and confirms the located text is exactly what the
        /// parser reported as the value. Start can point at an anchor or tag rather than the value
        /// itself (a: &amp;x nginx:1.21), and quoted values can carry escapes, so the region is only
        /// safe to replace once it has been checked against the value.
        /// </summary>
        static bool TryGetInlineValueRegion(string document, YamlScalarNode node, out int start, out int end)
        {
            start = 0;
            end = 0;

            if (node.Start.Line != node.End.Line)
                return false;

            var lineStart = OffsetOfLine(document, (int)node.Start.Line);
            var scalarStart = lineStart + (int)node.Start.Column - 1;

            switch (node.Style)
            {
                case ScalarStyle.Plain:
                    start = SkipNodeProperties(document, scalarStart);
                    end = lineStart + (int)node.End.Column - 1;
                    break;
                case ScalarStyle.SingleQuoted:
                case ScalarStyle.DoubleQuoted:
                    start = SkipNodeProperties(document, scalarStart) + 1;
                    end = lineStart + (int)node.End.Column - 2;
                    break;
                default:
                    return false;
            }

            return start >= 0
                   && end >= start
                   && end <= document.Length
                   && document[start..end] == node.Value;
        }

        /// <summary>
        /// Skips any anchor (&amp;name) and tag (!tag) properties preceding the value.
        /// </summary>
        static int SkipNodeProperties(string document, int index)
        {
            while (index < document.Length && (document[index] == '&' || document[index] == '!'))
            {
                while (index < document.Length && !char.IsWhiteSpace(document[index]))
                    index++;
                while (index < document.Length && (document[index] == ' ' || document[index] == '\t'))
                    index++;
            }

            return index;
        }

        public static string ReplaceValues(string document, IEnumerable<YamlScalarEdit> edits)
        {
            // An alias resolves to the same node object, so the same edit can be collected more than
            // once. Splicing it twice would apply the second edit at offsets the first has invalidated,
            // so keep one edit per node — by reference, since YamlScalarNode compares by value.
            var distinct = edits.Distinct(new NodeIdentityComparer());

            // Applying last-to-first keeps the offsets of the remaining, earlier edits valid.
            var ordered = distinct.OrderByDescending(e => e.Node.Start.Line)
                                  .ThenByDescending(e => e.Node.Start.Column);

            return ordered.Aggregate(document, Splice);
        }

        class NodeIdentityComparer : IEqualityComparer<YamlScalarEdit>
        {
            public bool Equals(YamlScalarEdit? x, YamlScalarEdit? y) => ReferenceEquals(x?.Node, y?.Node);

            public int GetHashCode(YamlScalarEdit edit) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(edit.Node);
        }

        static string Splice(string document, YamlScalarEdit edit)
        {
            return edit.Node.Style == ScalarStyle.Literal
                ? SpliceBlockScalar(document, edit.Node, edit.NewValue)
                : SpliceInlineScalar(document, edit.Node, edit.NewValue);
        }

        static string SpliceInlineScalar(string document, YamlScalarNode node, string newValue)
        {
            if (!TryGetInlineValueRegion(document, node, out var start, out var end))
                throw new NotSupportedException($"Cannot locate the value of this {node.Style} scalar to replace it. Check CanReplaceValue before creating an edit.");

            return document[..start] + newValue + document[end..];
        }

        /// <summary>
        /// A block scalar starts at its indicator (| or |- …) and its content is the indented lines
        /// that follow. End is column 1 of the line after the content, except when the block ends the
        /// file without a trailing newline, where it is the end of the last content line instead.
        /// </summary>
        static string SpliceBlockScalar(string document, YamlScalarNode node, string newValue)
        {
            var block = TryGetBlockRegion(document, node)
                        ?? throw new NotSupportedException("Cannot account for this block scalar's indentation to replace it. Check CanReplaceValue before creating an edit.");

            return document[..block.Start] + Render(newValue, block, document) + document[block.End..];
        }

        /// <summary>
        /// Renders a value as a block scalar's content: indented to match the block, using the
        /// document's line ending, and keeping the original region's trailing break or lack of one.
        /// </summary>
        static string Render(string value, BlockRegion block, string document)
        {
            var rendered = Reindent(value, block.Indent, document.DetectLineEnding() ?? "\n");

            return block.EndsWithBreak ? rendered : rendered.TrimEnd('\r', '\n');
        }

        static string Reindent(string value, string indent, string newLine)
        {
            var builder = new StringBuilder();
            foreach (var line in ContentLines(value))
            {
                if (line.Length > 0)
                    builder.Append(indent);
                builder.Append(line).Append(newLine);
            }

            return builder.ToString();
        }

        /// <summary>
        /// A clipped or kept block's value ends with a line break, which would otherwise yield a
        /// trailing empty element that is not a content line of its own.
        /// </summary>
        static IEnumerable<string> ContentLines(string value)
        {
            var lines = value.Split('\n');
            var count = lines.Length > 0 && lines[lines.Length - 1].Length == 0
                ? lines.Length - 1
                : lines.Length;

            return lines.Take(count).Select(line => line.TrimEnd('\r'));
        }

        /// <summary>
        /// The portion of a content line's leading whitespace that is structure rather than part of the
        /// string. YAML normally takes it from the first non-empty line, but an explicit indicator
        /// (|2) declares it, leaving any surplus as content. Deriving it from the difference between
        /// the raw line and the parsed line covers both without having to interpret the indicator.
        /// Returns null when the two do not correspond, so the caller refuses the block.
        /// </summary>
        static string? StructuralIndent(string region, string value)
        {
            var regionLines = ContentLines(region).ToList();
            var valueLines = ContentLines(value).ToList();

            for (var index = 0; index < valueLines.Count; index++)
            {
                if (valueLines[index].Length == 0)
                    continue;

                if (index >= regionLines.Count)
                    return null;

                var indentLength = regionLines[index].Length - valueLines[index].Length;
                if (indentLength < 0)
                    return null;

                var indent = regionLines[index][..indentLength];

                return indent.All(character => character == ' ' || character == '\t') ? indent : null;
            }

            return "";
        }

        /// <summary>
        /// Index of the first character of the given 1-based line, counting YAML line breaks
        /// (\r\n, \n and \r).
        /// </summary>
        static int OffsetOfLine(string document, int line)
        {
            var currentLine = 1;
            var index = 0;
            while (currentLine < line && index < document.Length)
            {
                var character = document[index++];
                if (character == '\r' && index < document.Length && document[index] == '\n')
                    index++;
                if (character == '\r' || character == '\n')
                    currentLine++;
            }

            return index;
        }
    }
}
