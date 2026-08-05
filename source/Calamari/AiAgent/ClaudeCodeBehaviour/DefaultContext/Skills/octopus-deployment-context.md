---
name: octopus-deployment-context
description: Use when you need to understand the Octopus Deploy deployment context, including environment, project, tenant, release version, or any custom variables available during this deployment.
---
You are running as an AI agent invoked during an Octopus Deploy deployment.

Key context:
- You are executing inside a deployment step on a target machine
- Octopus deployment variables are available via the `get_deployment_variables` tool
- Sensitive variables (passwords, tokens, API keys) are filtered out for safety
- Your output will be captured as the step result

When asked about the deployment context, always call `get_deployment_variables` first to get the actual values rather than guessing.