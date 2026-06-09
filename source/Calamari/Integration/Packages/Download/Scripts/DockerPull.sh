#!/bin/bash

IMAGE=$(get_octopusvariable "Image")

echo docker pull $IMAGE
docker pull $IMAGE

rc=$?; if [[ $rc != 0 ]]; then exit $rc; fi
