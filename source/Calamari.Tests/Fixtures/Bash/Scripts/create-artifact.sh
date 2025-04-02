#!/bin/bash

artifactPath=$(get_octopusvariable "BashFixture.ShouldCreateArtifact.Path")

new_octopusartifact "$artifactPath"