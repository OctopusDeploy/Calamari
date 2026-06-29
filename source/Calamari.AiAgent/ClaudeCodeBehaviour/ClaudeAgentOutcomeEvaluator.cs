using System;
using System.Linq;
using System.Text.RegularExpressions;
using Calamari.AiAgent.ClaudeCodeBehaviour.JsonResponseModels;
using Calamari.Common.Commands;

namespace Calamari.AiAgent.ClaudeCodeBehaviour
{
    public static class ClaudeAgentOutcomeEvaluator
    {
        // The agent emits this tagged block (see the octopus-fail-deployment skill) when a user-specified
        // failure condition has been met. The CLI still exits 0, so this is the only way to detect an intentional
        // failure from the outside. The inner text is the operator-facing reason. We accept either a self-closing
        // <octopus-task-failed/> (a complete signal with no reason) or a paired block ending in </octopus-task-failed>;
        // an opening tag without its closing tag is treated as a truncated message and ignored.
        static readonly Regex FailureSignal = new(
            @"<octopus-task-failed\s*(?:/>|>(?<reason>.*?)</octopus-task-failed>)",
            RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static void EnsureSuccessful(int exitCode, ResultStreamEvent? result)
        {
            if (exitCode != 0)
            {
                throw new CommandException($"Claude Code exited with code {exitCode}.");
            }

            if (result == null)
            {
                return;
            }

            // An intentional, user-requested failure takes precedence: the run is otherwise "successful" at the
            // CLI level, so check the agent's signal before the generic CLI-status checks to surface a clear reason.
            if (result.Result is { } text && FailureSignal.Match(text) is { Success: true } match)
            {
                var reason = match.Groups["reason"].Value.Trim();
                throw new CommandException(
                    string.IsNullOrEmpty(reason)
                        ? "The agent signalled that the step should fail."
                        : $"The agent signalled that the step should fail: {reason}");
            }

            if (result.IsError == true || !"success".Equals(result.Subtype, StringComparison.OrdinalIgnoreCase))
            {
                var subtype = string.IsNullOrEmpty(result.Subtype) ? "<none>" : result.Subtype;
                var stopReason = string.IsNullOrEmpty(result.StopReason) ? "<none>" : result.StopReason;
                throw new CommandException(
                    $"Claude Code did not complete successfully (subtype: {subtype}, stop_reason: {stopReason}).");
            }

            if (result.PermissionDenials is { Count: > 0 } denials)
            {
                var toolNames = string.Join(", ", denials.Select(d => string.IsNullOrEmpty(d.ToolName) ? "<unknown>" : d.ToolName));
                throw new CommandException(
                    $"Claude Code was denied permission to use the following tool(s): {toolNames}.");
            }
        }
    }
}
