$manifest = @"
"apiVersion": "v1"
"kind": "Namespace"
"metadata":
  "name": "example"
"labels":
    "name": "example"
---    
"apiVersion": "v1"
"kind": "Namespace"
"metadata":
  "name": "diffs"
"labels":
    "name": "diffs"
---
---
"@

Report-KubernetesManifest -manifest $manifest
$manifest | Report-KubernetesManifest -namespace "my"