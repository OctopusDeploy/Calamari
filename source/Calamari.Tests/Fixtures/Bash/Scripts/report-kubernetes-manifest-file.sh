#!/bin/bash
ManifestFilePath=$(get_octopusvariable "ManifestFilePath")
report_kubernetes_manifest_file "$ManifestFilePath"
report_kubernetes_manifest_file "$ManifestFilePath" "my"