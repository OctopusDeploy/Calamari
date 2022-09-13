#!/bin/bash

nginxTempDir=$(get_octopusvariable "OctopusNginxFeatureTempDirectory")

if [ -d "${nginxTempDir}" ]; then
    echo "Removing temporary folder ${nginxTempDir}..."
    sudo rm -rf $nginxTempDir
fi