---
name: octopus-fail-deployment
description: Use when the user's prompt asks for the step to FAIL under some condition — e.g. "fail the deployment if the health check is red", "if X happens, fail this step", "the runbook should fail when Y". By default an agent run always succeeds (the process exits 0), so the ONLY way to make Octopus mark this step as failed is to emit the sentinel described here. Do NOT use when the user has not expressed any failure condition — absence of the sentinel means success.
---
By default this step **succeeds** — when your run finishes normally, Octopus marks the step green regardless of what you found.

If the user's prompt states a condition under which the step should **fail** (for example "fail the deployment if the smoke test doesn't pass"), and you determine that condition has been met, you must explicitly signal the failure. The only way Octopus can detect this from the outside is a specific tagged block in your final response.

## How to signal failure

Emit this block as part of your **final** message, with the reason between the tags:

<octopus-task-failed>
A short reason describing why the step failed.
</octopus-task-failed>

For example:

<octopus-task-failed>
Smoke test returned HTTP 500 from /health after 3 retries.
</octopus-task-failed>

## Rules

- Emit the block **only** when the user expressed a failure condition AND you have determined it is met. If the condition was not met, say nothing special and let the step succeed.
- Always write a **complete** block — either a paired block ending in `</octopus-task-failed>` or a self-closing `<octopus-task-failed/>`. A closed tag is how Octopus confirms the message is whole — if you open the block but stop before closing it, the failure will not be detected, so finish the block before ending your turn.
- Put the tags on their **own lines**, as plain text. Do **not** wrap them in backticks, code fences, bold, or any other markdown.
- Emit the block **once**. One block is enough to fail the step.
- Keep the reason **concise and specific** — it is surfaced in the Octopus task log as the failure message, so write it for the operator who will read it. The reason may span multiple lines.
- The reason is optional but strongly encouraged; an empty block — or a self-closing `<octopus-task-failed/>` — will still fail the step with a generic message.
- If you cannot determine whether the condition was met, do not guess silently — explain what you found. Only emit the block if the user's intent was that an unverifiable outcome should fail the step.