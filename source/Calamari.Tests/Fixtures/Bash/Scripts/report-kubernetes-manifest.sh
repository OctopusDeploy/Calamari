#!/bin/bash
read -r -d '' manifest << EOM
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
EOM

report_kubernetes_manifest "$manifest"
report_kubernetes_manifest "$manifest" "my"