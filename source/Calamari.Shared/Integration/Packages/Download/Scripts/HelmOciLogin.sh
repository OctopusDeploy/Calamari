#!/bin/bash

helmUsername=$(get_octopusvariable "HelmUsername")
helmPassword=$(get_octopusvariable "HelmPassword")
feedUri=$(get_octopusvariable "FeedUri")

echo "##octopus[stdout-verbose]"
helm version
echo "##octopus[stdout-default]"

if [  ! -z $helmUsername ]; then
    
#    helmVersion=`helm version --template '{{.Version}}'`
#    parsedVersion=(${helmVersion/[v/.]/ })

    helm registry login $feedUri --username helmUsername --password helmPassword 2>&1
    rc=$?; if [[ $rc != 0 ]]; then
        echo "Login Failed" 
        exit $rc; 
    fi
fi