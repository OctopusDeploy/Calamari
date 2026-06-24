---
name: octopus-artifacts
description: Use ONLY when the user explicitly asks to attach, upload, or save output as an Octopus artifact. Do not infer artifacts from a request that merely creates files.
---
You can publish files you create as Octopus **artifacts** so they can be collected after this step.

Do this ONLY when the user explicitly asks to attach, upload, or save something as an artifact. Never infer it from a plain "create a file" request.

To publish artifacts:
1. Create the output **inside the current working directory** (this is your default directory). Do not write artifacts to `/tmp` or other locations.
2. Append one line per artifact to `.octopus/artifacts.jsonl` (create the `.octopus` directory and the file if they do not exist). Each line is a JSON object:
   `{"path": "<path relative to the working directory>", "name": "<optional display name>"}`

Rules:
- For several individual files, add **one line per file**.
- For many related files (for example a generated website), put them in a **dedicated subdirectory** and add a single line for that directory — it will be zipped into one artifact.
- Do **not** attach the working directory itself; always attach a specific file or subdirectory.
- `name` is optional; it defaults to the file name (or `<directory>.zip` for a directory).
