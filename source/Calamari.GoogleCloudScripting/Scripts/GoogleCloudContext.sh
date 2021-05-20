#!/bin/bash

Octopus_GoogleCloud_KeyFile=$(get_octopusvariable "OctopusGoogleCloudKeyFile")
gcloud_exe=$(get_octopusvariable "Octopus.Action.GoogleCloud.CustomExecutable")

function check_app_exists {
	command -v $1 > /dev/null 2>&1
	if [[ $? -ne 0 ]]; then
		echo >&2 "The executable $1 does not exist, or is not on the PATH."
		echo >&2 "You need $1 to be installed and in the PATH."
		exit 1
	fi
}

function set_executable {
  if [[ -z $gcloud_exe ]]; then
    gcloud_exe="gcloud"
  fi

  check_app_exists $gcloud_exe
}

function setup_context {
    {
        echo "##octopus[stdout-verbose]"

        export CLOUDSDK_CONFIG="$OctopusCalamariWorkingDirectory/gcloud-cli"
        mkdir -p $CLOUDSDK_CONFIG

        $gcloud_exe -q version
        echo "Google Cloud CLI: Authenticating with key file"
        loginArgs=()
        loginArgs+=("--key-file=$Octopus_GoogleCloud_KeyFile")
        echo gcloud auth activate-service-account ${loginArgs[@]}
        $gcloud_exe -q auth activate-service-account "${loginArgs[@]}"

        echo "##octopus[stdout-default]"
        write_verbose "Successfully authenticated with Google Cloud CLI"
    } || {
        # failed to authenticate with Google Cloud CLI
        write_error "Failed to authenticate with Google Cloud CLI"
        exit 1
    }
}

echo "##octopus[stdout-verbose]"
set_executable
setup_context

OctopusGoogleCloudTargetScript=$(get_octopusvariable "OctopusGoogleCloudTargetScript")
OctopusGoogleCloudTargetScriptParameters=$(get_octopusvariable "OctopusGoogleCloudTargetScriptParameters")

echo "Invoking target script \"$OctopusGoogleCloudTargetScript\" with $OctopusGoogleCloudTargetScriptParameters parameters"
echo "##octopus[stdout-default]"

source "$OctopusGoogleCloudTargetScript" $OctopusGoogleCloudTargetScriptParameters || exit 1 :
{
    $gcloud_exe -q auth revoke --all 2>null 3>null
}