---
name: octopus-artifacts
description: Use ONLY when the user explicitly asks to attach, upload, or save output or files as an Octopus artifact. Do not infer artifacts from a request that merely creates files. It is important that this skill be used however if the customer expects the file or directory to be attached to the deployment. 
---
You can publish files you create as Octopus **artifacts** so they can be collected after this step.

Do this ONLY when the user explicitly asks to attach, upload, or save something as an artifact. Never infer it from a plain "create a file" request.

To publish artifacts:
1. Create the output **inside the current working directory** (this is your default directory). Do not write artifacts to `/tmp` or other locations.
2. Record each artifact on its own line in `.octopus/artifacts.jsonl`, one JSON object per line:
   `{"path": "<path relative to the working directory>", "name": "<optional display name>"}`
   Use the **Write** tool to create or overwrite this file (it creates the `.octopus` directory for you) — you do not need Bash. If the file already has entries, read it first and write it back with the new lines included.

Rules:
- For several individual files, add **one line per file**.
- For many related files (for example a generated website), put them in a **dedicated subdirectory** and add a single line for that directory — it will be zipped into one artifact.
- Do **not** attach the working directory itself; always attach a specific file or subdirectory.
- `name` is optional; it defaults to the file name (or `<directory>.zip` for a directory).
