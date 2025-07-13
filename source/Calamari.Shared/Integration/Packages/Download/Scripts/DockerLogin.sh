#!/bin/bash

dockerUsername=$(get_octopusvariable "DockerUsername")
dockerPassword=$(get_octopusvariable "DockerPassword")
feedUri=$(get_octopusvariable "FeedUri")

echo "##octopus[stdout-verbose]"
docker -v
echo "##octopus[stdout-default]"

if [  ! -z $dockerUsername ]; then
    # docker 17.07 throws a warning to stderr if you use the --password param
    dockerVersion=`docker version --format '{{.Client.Version}}'`
    parsedVersion=(${dockerVersion//./ })

    if (( parsedVersion[0] > 17 || (parsedVersion[0] == 17 && parsedVersion[1] > 6) )); then
        echo $dockerPassword | docker login --username $dockerUsername --password-stdin $feedUri 2>&1
    else
        docker login --username $dockerUsername --password $dockerPassword $feedUri 2>&1
    fi
    rc=$?; if [[ $rc != 0 ]]; then
        echo "Login Failed" 
        exit $rc; 
    fi
fi