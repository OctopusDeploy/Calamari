#!/bin/bash

Octopus_Azure_Environment=$(get_octopusvariable "Octopus.Action.Azure.Environment")
Octopus_Azure_ADClientId=$(get_octopusvariable "Octopus.Action.Azure.ClientId")
Octopus_Azure_ADPassword=$(get_octopusvariable "Octopus.Action.Azure.Password")
Octopus_Azure_ADTenantId=$(get_octopusvariable "Octopus.Action.Azure.TenantId")
Octopus_Azure_SubscriptionId=$(get_octopusvariable "Octopus.Action.Azure.SubscriptionId")

function check_app_exists {
	command -v $1 > /dev/null 2>&1
	if [[ $? -ne 0 ]]; then
		echo >&2 "The executable $1 does not exist, or is not on the PATH."
		echo >&2 "See https://g.octopushq.com/TBD for more information."
		exit 1
	fi
}

function setup_context {
    {
        # authenticate with the Azure CLI
        echo "##octopus[stdout-verbose]"

        az cloud set --name ${Octopus_Azure_Environment:-"AzureCloud"} 2>null 3>null

        echo "Azure CLI: Authenticating with Service Principal"
        loginArgs=()
        loginArgs+=("-u $Octopus_Azure_ADClientId")
        # Use the full argument because of https://github.com/Azure/azure-cli/issues/12105
        loginArgs+=("--password $Octopus_Azure_ADPassword")
        loginArgs+=("--tenant $Octopus_Azure_ADTenantId")
        echo az login --service-principal ${loginArgs[@]}
        az login --service-principal ${loginArgs[@]}

        echo "Azure CLI: Setting active subscription to $Octopus_Azure_SubscriptionId"
        az account set --subscription $Octopus_Azure_SubscriptionId

        echo "##octopus[stdout-default]"
        write_verbose "Successfully authenticated with the Azure CLI"
    } || {
        # failed to authenticate with Azure CLI
        echo "Failed to authenticate with Azure CLI"
        exit 1
    }
}

echo "##octopus[stdout-verbose]"
check_app_exists az
setup_context

OctopusAzureTargetScript=$(get_octopusvariable "OctopusAzureTargetScript")
OctopusAzureTargetScriptParameters=$(get_octopusvariable "OctopusAzureTargetScriptParameters")

echo "Invoking target script \"$OctopusAzureTargetScript\" with $OctopusAzureTargetScriptParameters parameters"
echo "##octopus[stdout-default]"

source "$OctopusAzureTargetScript" $OctopusAzureTargetScriptParameters || exit 1 :
{
    az logout 2>null 3>null
}