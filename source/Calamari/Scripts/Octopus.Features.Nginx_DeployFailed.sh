#!/bin/bash

nginxTempDir=$(get_octopusvariable "OctopusNginxFeatureTempDirectory")

echo "Removing temporary folder ${nginxTempDir}..."
sudo rm -rf $nginxTempDir