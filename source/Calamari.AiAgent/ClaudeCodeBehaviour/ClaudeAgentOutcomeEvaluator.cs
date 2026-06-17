using System;
using System.Linq;
using Calamari.AiAgent.ClaudeCodeBehaviour.JsonResponseModels;
using Calamari.Common.Commands;

namespace Calamari.AiAgent.ClaudeCodeBehaviour
{
    public class ClaudeAgentOutcomeEvaluator
    {
        public void EnsureSuccessful(int exitCode, ResultStreamEvent? result)
        {
            if (exitCode != 0)
            {
                throw new CommandException($"Claude Code exited with code {exitCode}.");
            }

            // Exit code 0 but no terminal result event: the CLI reported success at the process level, but we
            // couldn't observe a result to inspect (output-format drift or an unparseable result line). We have
            // no failure signal beyond the exit code, so we defer to it rather than fail on a parsing gap.
            if (result == null)
            {
                return;
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
