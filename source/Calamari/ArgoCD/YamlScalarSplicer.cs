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
        public static string ReplaceValue(string document, YamlScalarNode node, string newValue)
        {
            return ReplaceValues(document, new[] { new YamlScalarEdit(node, newValue) });
        }

        public static string ReplaceValues(string document, IEnumerable<YamlScalarEdit> edits)
        {
            // Applying last-to-first keeps the offsets of the remaining, earlier edits valid.
            var ordered = edits.OrderByDescending(e => e.Node.Start.Line)
                               .ThenByDescending(e => e.Node.Start.Column);

            return ordered.Aggregate(document, Splice);
        }

        static string Splice(string document, YamlScalarEdit edit)
        {
            return edit.Node.Style == ScalarStyle.Literal
                ? SpliceBlockScalar(document, edit.Node, edit.NewValue)
                : SpliceInlineScalar(document, edit.Node, edit.NewValue);
        }

        static string SpliceInlineScalar(string document, YamlScalarNode node, string newValue)
        {
            var (startColumn, endColumn) = InlineValueColumns(node);
            var start = OffsetOfLine(document, (int)node.Start.Line) + startColumn;
            var end = OffsetOfLine(document, (int)node.End.Line) + endColumn;

            return document[..start] + newValue + document[end..];
        }

        /// <summary>
        /// Columns bounding the value itself, excluding any quotes so they are left in place.
        /// </summary>
        static (int startColumn, int endColumn) InlineValueColumns(YamlScalarNode node)
        {
            switch (node.Style)
            {
                case ScalarStyle.Plain:
                    return ((int)node.Start.Column - 1, (int)node.End.Column - 1);
                case ScalarStyle.SingleQuoted:
                case ScalarStyle.DoubleQuoted:
                    return ((int)node.Start.Column, (int)node.End.Column - 2);
                default:
                    throw new NotSupportedException($"Replacing the value of a {node.Style} scalar is not supported.");
            }
        }

        /// <summary>
        /// A block scalar starts at its indicator (| or |- …) and its content is the indented lines
        /// that follow. End is column 1 of the line after the content, except when the block ends the
        /// file without a trailing newline, where it is the end of the last content line instead.
        /// </summary>
        static string SpliceBlockScalar(string document, YamlScalarNode node, string newValue)
        {
            var contentStart = OffsetOfLine(document, (int)node.Start.Line + 1);
            var contentEnd = OffsetOfLine(document, (int)node.End.Line) + (int)node.End.Column - 1;
            var originalContent = document[contentStart..contentEnd];

            var replacement = Reindent(newValue, BlockIndent(originalContent), document.DetectLineEnding() ?? "\n");
            if (!EndsWithLineBreak(originalContent))
                replacement = replacement.TrimEnd('\r', '\n');

            return document[..contentStart] + replacement + document[contentEnd..];
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

        static string BlockIndent(string content)
        {
            var firstContentLine = content.Split('\n')
                                          .FirstOrDefault(line => line.Trim('\r').Trim().Length > 0)
                                   ?? "";

            return firstContentLine[..(firstContentLine.Length - firstContentLine.TrimStart(' ', '\t').Length)];
        }

        static bool EndsWithLineBreak(string content)
        {
            return content.EndsWith("\n") || content.EndsWith("\r");
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
