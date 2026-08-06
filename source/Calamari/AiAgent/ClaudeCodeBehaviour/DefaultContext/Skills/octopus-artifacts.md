---
name: octopus-artifacts
description: Use when the user wants a file, directory, or saved command output attached, uploaded, published, or made downloadable as an Octopus artifact — i.e. they expect it to surface in Octopus after this step (in the deployment/task artifacts), not just exist on the worker. Look for keywords "Octopus artifact", "attach/upload to the deployment", "publish to the release", "make this downloadable in Octopus". Do NOT use when the request is only to create, generate, or write a file locally — producing a file is not attaching an artifact unless the user explicitly wants it available in Octopus. 
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
