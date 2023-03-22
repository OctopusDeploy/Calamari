#!/bin/bash

IMAGE=$(get_octopusvariable "Image")
VERSION=$(get_octopusvariable "Version")

echo helm pull $IMAGE --version $VERSION
helm pull $IMAGE --version $VERSION

rc=$?; if [[ $rc != 0 ]]; then exit $rc; fi
